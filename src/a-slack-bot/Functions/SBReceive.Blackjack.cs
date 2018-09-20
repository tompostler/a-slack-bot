﻿using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SBReceive
    {
        [FunctionName(nameof(SBReceiveBlackjack))]
        public static async Task SBReceiveBlackjack(
            [ServiceBusTrigger(C.SBQ.Blackjack)]Messages.ServiceBusBlackjack inMessage,
            [ServiceBus(C.SBQ.SendMessage)]IAsyncCollector<Slack.Events.Inner.message> messageCollector,
            [ServiceBus(C.SBQ.Blackjack)]IAsyncCollector<BrokeredMessage> messageStateCollector,
            [DocumentDB(ConnectionStringSetting = C.CDB.CSS)]DocumentClient docClient,
            ILogger logger)
        {
            if (Settings.Debug)
                logger.LogInformation("Msg: {0}", JsonConvert.SerializeObject(inMessage));

            await SR.Init(logger);

            var gameDocUri = UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, $"{inMessage.channel_id}|{inMessage.thread_ts}");

            // Handle a balance request
            if (inMessage.type == Messages.BlackjackMessageType.GetBalance || inMessage.type == Messages.BlackjackMessageType.GetBalances)
            {
                try
                {
                    var gameBalancesDoc = await docClient.ReadDocumentAsync<Documents.BlackjackStandings>(Documents.BlackjackStandings.DocUri, new RequestOptions { PartitionKey = Documents.Blackjack.PartitionKey });

                    if (inMessage.type == Messages.BlackjackMessageType.GetBalance)
                        if (gameBalancesDoc.Document.Content.ContainsKey(inMessage.user_id))
                            await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = inMessage.channel_id, text = $"<@{inMessage.user_id}>: ¤{gameBalancesDoc.Document.Content[inMessage.user_id]}" });
                        else
                            await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = inMessage.channel_id, text = $"<@{inMessage.user_id}>: ¤1,000,000" });
                    else
                    {
                        // all balances
                        var sb = new StringBuilder();
                        sb.AppendLine("Balances for those that have played:");
                        sb.AppendLine("```");
                        var maxNamLength = Math.Max(SR.U.MaxNameLength, 4);
                        var maxBalLength = Math.Max($"{(gameBalancesDoc.Document.Content.Values.Count == 0 ? 0 : gameBalancesDoc.Document.Content.Values.Max()):#,#}".Length, 7);
                        sb.Append("USER".PadRight(SR.U.MaxNameLength));
                        sb.Append("  ");
                        sb.Append("BALANCE".PadLeft(maxBalLength));
                        sb.AppendLine();
                        foreach (var user in gameBalancesDoc.Document.Content)
                        {
                            if (SR.U.IdToName.ContainsKey(user.Key))
                                sb.Append($"{SR.U.IdToName[user.Key].PadRight(maxNamLength)}  ");
                            else
                                sb.Append($"{user.Key.PadRight(maxNamLength)}  ");
                            sb.AppendFormat($"{{0,{maxBalLength}:#,#}}", user.Value);
                            sb.AppendLine();
                        }
                        sb.AppendLine("```");
                        sb.AppendLine();
                        sb.AppendLine("Balances for those that have not played: ¤1,000,000");
                        await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = inMessage.channel_id, text = sb.ToString() });
                    }
                    return;
                }
                catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
                {
                    await docClient.CreateDocumentAsync(
                        Documents.Blackjack.DocColUri,
                        new Documents.BlackjackStandings { Content = new Dictionary<string, long>() },
                        new RequestOptions { PartitionKey = Documents.Blackjack.PartitionKey });

                    // Let SB retry us. Should only ever hit this once.
                    throw;
                }
            }

            // Get the game doc
            var gameDoc = await docClient.ReadDocumentAsync<Documents.Blackjack>(gameDocUri, new RequestOptions { PartitionKey = Documents.Blackjack.PartitionKey });
            logger.LogInformation("Got game doc");

            switch (inMessage.type)
            {
                case Messages.BlackjackMessageType.UpdateBalance:
                    var gameBalancesDoc = await docClient.ReadDocumentAsync<Documents.BlackjackStandings>(
                        Documents.BlackjackStandings.DocUri,
                        new RequestOptions { PartitionKey = Documents.Blackjack.PartitionKey });
                    var bals = gameBalancesDoc.Document.Content;
                    if (!bals.ContainsKey(inMessage.user_id))
                    {
                        bals[inMessage.user_id] = 1_000_000;
                        logger.LogInformation("{0} didn't have money. Initial balance set.", inMessage.user_id);
                    }
                    bals[inMessage.user_id] += inMessage.amount;
                    if (bals[inMessage.user_id] <= 0)
                    {
                        bals[inMessage.user_id] = 1;
                        await messageCollector.SendMessageAsync(inMessage, $"<@{inMessage.user_id}> is so poor their balance was forced to ¤1.");
                    }
                    await docClient.UpsertDocumentAsync(
                        Documents.Blackjack.DocColUri,
                        gameBalancesDoc,
                        new RequestOptions
                        {
                            AccessCondition = new AccessCondition
                            {
                                Condition = gameBalancesDoc.Document.ETag,
                                Type = AccessConditionType.IfMatch
                            },
                            PartitionKey = Documents.Blackjack.PartitionKey
                        },
                        disableAutomaticIdGeneration: true);
                    break;


                case Messages.BlackjackMessageType.Timer_Joining:
                    if (gameDoc.Document.state == Documents.BlackjackGameState.Joining)
                    {
                        await messageCollector.SendMessageAsync(inMessage, "Joining timed out.");
                        var msg = new BrokeredMessage(new Messages.ServiceBusBlackjack { channel_id = inMessage.channel_id, thread_ts = inMessage.thread_ts, type = Messages.BlackjackMessageType.Timer_CollectingBets })
                        {
                            ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddMinutes(1)
                        };
                        await messageStateCollector.AddAsync(msg);
                        logger.LogInformation("New timer message for 1 minute.");
                        goto case Messages.BlackjackMessageType.ToCollectingBets;
                    }
                    logger.LogInformation("Game state no longer joining. Timer ignored.");
                    break;


                case Messages.BlackjackMessageType.Timer_CollectingBets:
                    if (gameDoc.Document.state == Documents.BlackjackGameState.CollectingBets)
                    {
                        // users are only added to bets if they bet
                        if (gameDoc.Document.bets.Count != gameDoc.Document.hands.Count)
                        {
                            var chuckleHeads = gameDoc.Document.hands.Keys.Except(gameDoc.Document.bets.Keys).ToList();

                            for (int i = 0; i < chuckleHeads.Count; i++)
                            {
                                var chuckleHead = chuckleHeads[i];
                                await messageCollector.SendMessageAsync(inMessage, $"Betting timed out. Dropping <@{chuckleHead}> who loses ¤1 as a penalty for not betting.");
                                var chuckleMessage = new BrokeredMessage(new Messages.ServiceBusBlackjack { type = Messages.BlackjackMessageType.UpdateBalance, channel_id = inMessage.channel_id, thread_ts = inMessage.thread_ts, user_id = chuckleHead, amount = -1 })
                                {
                                    ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddSeconds(2 * i)
                                };
                                await messageStateCollector.AddAsync(chuckleMessage);
                            }
                        }
                        goto case Messages.BlackjackMessageType.ToGame;
                    }
                    logger.LogInformation("Game state no longer collecting bets. Timer ignored.");
                    break;


                case Messages.BlackjackMessageType.JoinGame:
                    gameDoc.Document.moves.Add(new Documents.BlackjackMove { action = Documents.BlackjackAction.Join, user_id = inMessage.user_id });
                    gameDoc.Document.hands.Add(inMessage.user_id, new List<string>());
                    await messageCollector.SendMessageAsync(inMessage, $"Thanks for joining {SR.U.IdToName[inMessage.user_id]}");
                    await UpsertGameDocument(docClient, gameDoc);
                    break;


                case Messages.BlackjackMessageType.ToCollectingBets:
                    gameDoc.Document.moves.Add(new Documents.BlackjackMove { action = Documents.BlackjackAction.StateChange, user_id = inMessage.user_id, to_state = Documents.BlackjackGameState.CollectingBets });
                    gameDoc.Document.state = Documents.BlackjackGameState.CollectingBets;
                    await UpsertGameDocument(docClient, gameDoc);
                    await messageCollector.SendMessageAsync(inMessage, "Collecting bets!");
                    logger.LogInformation("Updated game state to collecting bets.");
                    break;


                case Messages.BlackjackMessageType.PlaceBet:
                    gameDoc.Document.moves.Add(new Documents.BlackjackMove { action = Documents.BlackjackAction.Bet, user_id = inMessage.user_id, bet = inMessage.amount });
                    gameDoc.Document.bets.Add(inMessage.user_id, inMessage.amount);
                    await UpsertGameDocument(docClient, gameDoc);
                    await messageCollector.SendMessageAsync(inMessage, $"{SR.U.IdToName[inMessage.user_id]} bets ¤{inMessage.amount}");
                    logger.LogInformation("Updated game with bet.");

                    if (gameDoc.Document.bets.Count == gameDoc.Document.hands.Count)
                        goto case Messages.BlackjackMessageType.ToGame;
                    break;


                case Messages.BlackjackMessageType.ToGame:
                    gameDoc.Document.moves.Add(new Documents.BlackjackMove { action = Documents.BlackjackAction.StateChange, to_state = Documents.BlackjackGameState.Running });
                    gameDoc.Document.state = Documents.BlackjackGameState.Running;
                    await UpsertGameDocument(docClient, gameDoc);
                    await messageCollector.SendMessageAsync(inMessage, $"Running a game not yet supported. All bets cancelled. {inMessage.channel_id}|{inMessage.thread_ts}");
                    logger.LogInformation("Updated game state to running.");
                    break;


                default:
                    await messageCollector.SendMessageAsync(inMessage, $"NOT SUPPORTED YET: {inMessage.channel_id}|{inMessage.thread_ts}");
                    break;
            }
        }

        private static Task UpsertGameDocument(DocumentClient docClient, Documents.Blackjack gameDoc)
        {
            return docClient.UpsertDocumentAsync(
                Documents.Blackjack.DocColUri,
                gameDoc,
                new RequestOptions
                {
                    AccessCondition = new AccessCondition
                    {
                        Condition = gameDoc.ETag,
                        Type = AccessConditionType.IfMatch
                    },
                    PartitionKey = Documents.Blackjack.PartitionKey
                },
                disableAutomaticIdGeneration: true);
        }
    }
}
