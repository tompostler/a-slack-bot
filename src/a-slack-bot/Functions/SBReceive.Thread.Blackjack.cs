using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SBReceive
    {
        private static async Task SBReceiveThreadBlackjack(
            DocumentClient docClient,
            Documents.Blackjack gameDoc,
            Slack.Events.Inner.message message,
            IAsyncCollector<Messages.ServiceBusBlackjack> blackjackCollector,
            ILogger logger)
        {
            switch (gameDoc.state)
            {
                case Documents.BlackjackGameState.Joining:
                    if (message.text.Trim().ToLowerInvariant() == "join" && !gameDoc.users.Contains(message.user))
                        await blackjackCollector.AddAsync(new Messages.ServiceBusBlackjack { type = Messages.BlackjackMessageType.JoinGame, channel_id = message.channel, thread_ts = message.thread_ts, user_id = message.user });
                    else if (message.text.Trim().ToLowerInvariant() == "start" && gameDoc.users.Contains(message.user))
                        await blackjackCollector.AddAsync(new Messages.ServiceBusBlackjack { type = Messages.BlackjackMessageType.ToCollectingBets, channel_id = message.channel, thread_ts = message.thread_ts, user_id = message.user });
                    break;

                case Documents.BlackjackGameState.CollectingBets:
                    if (!gameDoc.bets.ContainsKey(message.user) && ((long.TryParse(message.text, out long bet) && bet > 0) || message.text.ToLower() == "all"))
                    {
                        var gameBalancesDoc = await docClient.ReadDocumentAsync<Documents.BlackjackStandings>(
                            Documents.BlackjackStandings.DocUri,
                            new RequestOptions { PartitionKey = Documents.Blackjack.PartitionKey });
                        var standings = gameBalancesDoc.Document.Content;
                        if (message.text.ToLower() == "all")
                            bet = standings.ContainsKey(message.user) ? standings[message.user] : 10_000;
                        if ((standings.ContainsKey(message.user) && bet <= standings[message.user]) || bet <= 10_000)
                            await blackjackCollector.AddAsync(new Messages.ServiceBusBlackjack { type = Messages.BlackjackMessageType.PlaceBet, channel_id = message.channel, thread_ts = message.thread_ts, user_id = message.user, amount = bet });
                    }
                    break;

                case Documents.BlackjackGameState.Running:
                    if (gameDoc.user_active < gameDoc.users.Count && gameDoc.users[gameDoc.user_active] == message.user)
                    {
                        Documents.BlackjackActionType action = Documents.BlackjackActionType.Invalid;
                        switch (message.text.Trim().ToLowerInvariant())
                        {
                            case "hit":
                            case "stand":
                            case "double":
                            case "split":
                            case "surrender":
                                action = (Documents.BlackjackActionType)Enum.Parse(typeof(Documents.BlackjackActionType), message.text.Trim(), ignoreCase: true);
                                break;
                            default:
                                logger.LogInformation($"{message.user} said '{message.text}' which is not a valid action.");
                                return;
                        }
                        // Throw away invalid game state choices. We'll only be in here if the user is currently the active player,
                        //  and we only need to validate the three actions that aren't available after two cards
                        if (gameDoc.hands[message.user].Count > 2 && (action == Documents.BlackjackActionType.Double || action == Documents.BlackjackActionType.Split || action == Documents.BlackjackActionType.Surrender))
                            break;
                        await blackjackCollector.AddAsync(new Messages.ServiceBusBlackjack { type = Messages.BlackjackMessageType.GameAction, channel_id = message.channel, thread_ts = message.thread_ts, user_id = message.user, action = action });
                    }
                    break;
            }
        }
    }
}
