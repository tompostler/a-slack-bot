using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
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
                    if (message.text.Contains("join") && !gameDoc.hands.ContainsKey(message.user))
                        await blackjackCollector.AddAsync(new Messages.ServiceBusBlackjack { type = Messages.BlackjackMessageType.JoinGame, channel_id = message.channel, thread_ts = message.thread_ts, user_id = message.user });
                    else if (message.text.Contains("start") && gameDoc.hands.ContainsKey(message.user))
                        await blackjackCollector.AddAsync(new Messages.ServiceBusBlackjack { type = Messages.BlackjackMessageType.ToCollectingBets, channel_id = message.channel, thread_ts = message.thread_ts, user_id = message.user });
                    break;

                case Documents.BlackjackGameState.CollectingBets:
                    if (!gameDoc.bets.ContainsKey(message.user) && long.TryParse(message.text, out long bet) && bet > 0)
                    {
                        var gameBalancesDoc = await docClient.ReadDocumentAsync<Documents.BlackjackStandings>(
                            Documents.BlackjackStandings.DocUri,
                            new RequestOptions { PartitionKey = Documents.Blackjack.PartitionKey });
                        var standings = gameBalancesDoc.Document.Content;
                        if ((standings.ContainsKey(message.user) && bet <= standings[message.user]) || bet < 1_000_000)
                            await blackjackCollector.AddAsync(new Messages.ServiceBusBlackjack { type = Messages.BlackjackMessageType.PlaceBet, channel_id = message.channel, thread_ts = message.thread_ts, user_id = message.user, amount = bet });
                    }
                    break;
            }
        }
    }
}
