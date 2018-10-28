using Microsoft.Azure.Documents;
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
            Documents.BlackjackStandings gameBalancesDoc = null;
            try
            {
                gameBalancesDoc = await docClient.ReadDocumentAsync<Documents.BlackjackStandings>(Documents.BlackjackStandings.DocUri, new RequestOptions { PartitionKey = Documents.Blackjack.PartitionKey });
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

            // Handle a balance request
            if (inMessage.type == Messages.BlackjackMessageType.GetBalance || inMessage.type == Messages.BlackjackMessageType.GetBalances)
            {
                if (inMessage.type == Messages.BlackjackMessageType.GetBalance)
                    if (gameBalancesDoc.Content.ContainsKey(inMessage.user_id))
                        await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = inMessage.channel_id, text = $"<@{inMessage.user_id}>: ¤{gameBalancesDoc.Content[inMessage.user_id]}" });
                    else
                        await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = inMessage.channel_id, text = $"<@{inMessage.user_id}>: ¤1,000,000" });
                else
                {
                    // all balances
                    var sb = new StringBuilder();
                    sb.AppendLine("Balances for those that have played:");
                    sb.AppendLine("```");
                    var maxNamLength = Math.Max(SR.U.MaxNameLength, 8);
                    var maxBalLength = Math.Max($"{(gameBalancesDoc.Content.Values.Count == 0 ? 0 : gameBalancesDoc.Content.Values.Max()):#,#}".Length, 7);
                    sb.Append("USER".PadRight(SR.U.MaxNameLength));
                    sb.Append("  ");
                    sb.Append("BALANCE".PadLeft(maxBalLength));
                    sb.AppendLine();
                    foreach (var user in gameBalancesDoc.Content)
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

            // Get the game doc
            Documents.Blackjack gameDoc = await docClient.ReadDocumentAsync<Documents.Blackjack>(gameDocUri, new RequestOptions { PartitionKey = Documents.Blackjack.PartitionKey });
            logger.LogInformation("Got game doc");

            switch (inMessage.type)
            {
                case Messages.BlackjackMessageType.UpdateBalance:
                    var bals = gameBalancesDoc.Content;
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
                                Condition = gameBalancesDoc.ETag,
                                Type = AccessConditionType.IfMatch
                            },
                            PartitionKey = Documents.Blackjack.PartitionKey
                        },
                        disableAutomaticIdGeneration: true);
                    gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.BalanceChange, user_id = inMessage.user_id, amount = inMessage.amount });
                    gameDoc = await UpsertGameDocument(docClient, gameDoc);
                    break;


                case Messages.BlackjackMessageType.Timer_Joining:
                    if (gameDoc.state == Documents.BlackjackGameState.Joining)
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
                    if (gameDoc.state == Documents.BlackjackGameState.CollectingBets)
                    {
                        // users are only added to bets if they bet
                        if (gameDoc.bets.Count != gameDoc.users.Count)
                        {
                            var chuckleHeads = gameDoc.users.Except(gameDoc.bets.Keys).ToList();

                            for (int i = 0; i < chuckleHeads.Count; i++)
                            {
                                var chuckleHead = chuckleHeads[i];
                                long balance = 1_000_000;
                                if (gameBalancesDoc.Content.ContainsKey(chuckleHead))
                                    balance = gameBalancesDoc.Content[chuckleHead];
                                // Lose at most 2.5% of total balance
                                var losspct = SR.Rand.NextDouble() * 0.025;
                                logger.LogInformation("Loss percent {0} for {1}", losspct, chuckleHead);
                                var loss = (long)Math.Max(losspct * balance, 1);
                                gameDoc.users.Remove(chuckleHead);
                                gameDoc.hands.Remove(chuckleHead);
                                gameDoc = await UpsertGameDocument(docClient, gameDoc);
                                await messageCollector.SendMessageAsync(inMessage, $"Betting timed out. Dropped <@{chuckleHead}> who loses ¤{loss:#,#} ({losspct:p}) as a penalty for not betting.");
                                var chuckleMessage = new BrokeredMessage(new Messages.ServiceBusBlackjack { type = Messages.BlackjackMessageType.UpdateBalance, channel_id = inMessage.channel_id, thread_ts = inMessage.thread_ts, user_id = chuckleHead, amount = -loss })
                                {
                                    // Schedule every 2s to give cosmos db a chance
                                    ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddSeconds(2 * i)
                                };
                                await messageStateCollector.AddAsync(chuckleMessage);
                            }
                        }
                        if (gameDoc.users.Count == 0)
                            goto case Messages.BlackjackMessageType.ToFinish;
                        else
                            goto case Messages.BlackjackMessageType.ToGame;
                    }
                    logger.LogInformation("Game state no longer collecting bets. Timer ignored.");
                    break;


                case Messages.BlackjackMessageType.Timer_Running:
                    if (gameDoc.state == Documents.BlackjackGameState.Running && gameDoc.user_active < gameDoc.users.Count && gameDoc.users[gameDoc.user_active] == inMessage.user_id)
                    {
                        await SendMessageAsync(messageCollector, inMessage, $"<@{inMessage.user_id}> loses ¤{gameDoc.bets[inMessage.user_id] * 2:#,#} for not completing their game in time.");
                        await messageStateCollector.AddAsync(new BrokeredMessage(new Messages.ServiceBusBlackjack { type = Messages.BlackjackMessageType.UpdateBalance, channel_id = inMessage.channel_id, thread_ts = inMessage.thread_ts, user_id = inMessage.user_id, amount = -(gameDoc.bets[inMessage.user_id] * 2) }));
                        gameDoc.users.Remove(inMessage.user_id);
                        gameDoc.user_active--;
                        gameDoc = await UpsertGameDocument(docClient, gameDoc);
                        await QueueNextPlayer(inMessage, gameDoc, messageStateCollector);
                    }
                    logger.LogInformation("Game state no longer waiting on that user. Timer ignored.");
                    break;


                case Messages.BlackjackMessageType.JoinGame:
                    if (!gameDoc.users.Contains(inMessage.user_id) && gameDoc.state == Documents.BlackjackGameState.Joining)
                    {
                        gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.Join, user_id = inMessage.user_id });
                        gameDoc.users.Add(inMessage.user_id);
                        gameDoc.hands.Add(inMessage.user_id, new List<Cards.Cards>());
                        await messageCollector.SendMessageAsync(inMessage, $"Thanks for joining, {SR.U.IdToName[inMessage.user_id]}.");
                        gameDoc = await UpsertGameDocument(docClient, gameDoc);
                    }
                    break;


                case Messages.BlackjackMessageType.ToCollectingBets:
                    gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.StateChange, user_id = inMessage.user_id, to_state = Documents.BlackjackGameState.CollectingBets });
                    gameDoc.state = Documents.BlackjackGameState.CollectingBets;
                    gameDoc = await UpsertGameDocument(docClient, gameDoc);
                    await messageCollector.SendMessageAsync(inMessage, "Collecting bets! Pays 1:1 or 3:2 on blackjack. Timing out in 1 minute.");
                    logger.LogInformation("Updated game state to collecting bets.");
                    break;


                case Messages.BlackjackMessageType.PlaceBet:
                    if (!gameDoc.bets.ContainsKey(inMessage.user_id) && gameDoc.state == Documents.BlackjackGameState.CollectingBets)
                    {
                        gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.Bet, user_id = inMessage.user_id, amount = inMessage.amount });
                        gameDoc.bets.Add(inMessage.user_id, inMessage.amount);
                        gameDoc = await UpsertGameDocument(docClient, gameDoc);
                        await messageCollector.SendMessageAsync(inMessage, $"{SR.U.IdToName[inMessage.user_id]} bets ¤{inMessage.amount:#,#}");
                        logger.LogInformation("Updated game with bet.");

                        if (gameDoc.bets.Count == gameDoc.users.Count)
                            goto case Messages.BlackjackMessageType.ToGame;
                    }
                    break;


                case Messages.BlackjackMessageType.ToGame:
                    // Update to running game
                    gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.StateChange, to_state = Documents.BlackjackGameState.Running });
                    gameDoc.state = Documents.BlackjackGameState.Running;
                    gameDoc = await UpsertGameDocument(docClient, gameDoc);
                    await messageCollector.SendMessageAsync(inMessage, "Running a game is currently experimental. Good luck!");
                    logger.LogInformation("Updated game state to running.");

                    // Deal first two cards to everybody
                    Cards.Deck deck = new Cards.Deck();
                    foreach (var hand in gameDoc.hands)
                    {
                        gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.Deal, user_id = hand.Key, card = deck.Deal() });
                        hand.Value.Add(gameDoc.actions.Last().card.Value);
                        gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.Deal, user_id = hand.Key, card = deck.Deal() });
                        hand.Value.Add(gameDoc.actions.Last().card.Value);
                    }
                    gameDoc.hands.Add("dealer", new List<Cards.Cards>());
                    gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.Deal, user_id = "dealer", card = deck.Deal() });
                    gameDoc.hands["dealer"].Add(gameDoc.actions.Last().card.Value);
                    gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.Deal, user_id = "dealer", card = deck.Deal() });
                    gameDoc.hands["dealer"].Add(gameDoc.actions.Last().card.Value);
                    gameDoc.deck = deck;
                    gameDoc = await UpsertGameDocument(docClient, gameDoc);
                    await ShowGameState(messageCollector, inMessage, gameDoc);

                    // Check for dealer natural
                    var dealerScore = Cards.CardHelpers.GetBlackjackScore(gameDoc.hands["dealer"]);
                    if (dealerScore.IsBlackjack)
                        goto case Messages.BlackjackMessageType.ToFinish;

                    // Queue up first player
                    await messageStateCollector.AddAsync(new BrokeredMessage(new Messages.ServiceBusBlackjack
                    {
                        type = Messages.BlackjackMessageType.GameAction,
                        channel_id = inMessage.channel_id,
                        thread_ts = inMessage.thread_ts,
                        action = Documents.BlackjackActionType.Prompt,
                        user_id = gameDoc.users[0]
                    }));

                    break;


                case Messages.BlackjackMessageType.GameAction:
                    gameDoc.actions.Add(new Documents.BlackjackAction { type = inMessage.action, user_id = inMessage.user_id });
                    Cards.Deck gameDeck = gameDoc.deck;
                    Cards.CardHelpers.BlackjackScore score = Cards.CardHelpers.GetBlackjackScore(gameDoc.hands[inMessage.user_id]);
                    var sb = new StringBuilder();
                    switch (inMessage.action)
                    {
                        case Documents.BlackjackActionType.Prompt:
                            if (score.IsBlackjack)
                            {
                                sb.AppendFormat("<@{0}> blackjack!", inMessage.user_id);
                                await QueueNextPlayer(inMessage, gameDoc, messageStateCollector);
                                break;
                            }
                            sb.AppendFormat("<@{0}>'s turn:", inMessage.user_id);
                            sb.AppendLine();
                            AddHandToGameState(sb, gameDoc.hands[inMessage.user_id]);
                            sb.AppendLine("Pick: `hit` `stand` `double` `surrender`");
                            await messageStateCollector.AddAsync(
                                new BrokeredMessage(
                                    new Messages.ServiceBusBlackjack
                                    {
                                        type = Messages.BlackjackMessageType.Timer_Running,
                                        channel_id = inMessage.channel_id,
                                        thread_ts = inMessage.thread_ts,
                                        user_id = inMessage.user_id
                                    })
                                {
                                    ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddMinutes(5)
                                });
                            break;

                        case Documents.BlackjackActionType.Hit:
                            gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.Deal, user_id = inMessage.user_id, card = gameDeck.Deal() });
                            gameDoc.hands[inMessage.user_id].Add(gameDoc.actions.Last().card.Value);
                            score = Cards.CardHelpers.GetBlackjackScore(gameDoc.hands[inMessage.user_id]);
                            AddHandToGameState(sb, gameDoc.hands[inMessage.user_id]);
                            if (score.IsBust)
                            {
                                sb.AppendFormat("{0} is bust!", SR.U.IdToName[inMessage.user_id]);
                                sb.AppendLine();
                                await QueueNextPlayer(inMessage, gameDoc, messageStateCollector);
                            }
                            else
                                sb.AppendLine("Pick: `hit` `stand`");
                            break;

                        case Documents.BlackjackActionType.Stand:
                            await QueueNextPlayer(inMessage, gameDoc, messageStateCollector);
                            break;

                        case Documents.BlackjackActionType.Double:
                            gameDoc.bets[inMessage.user_id] *= 2;
                            sb.AppendFormat("{0}'s bet doubled to ¤{1:#,#}", SR.U.IdToName[inMessage.user_id], gameDoc.bets[inMessage.user_id]);
                            sb.AppendLine();
                            gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.Deal, user_id = inMessage.user_id, card = gameDeck.Deal() });
                            gameDoc.hands[inMessage.user_id].Add(gameDoc.actions.Last().card.Value);
                            score = Cards.CardHelpers.GetBlackjackScore(gameDoc.hands[inMessage.user_id]);
                            AddHandToGameState(sb, gameDoc.hands[inMessage.user_id]);
                            if (score.IsBust)
                            {
                                sb.AppendFormat("{0} is bust!", SR.U.IdToName[inMessage.user_id]);
                                sb.AppendLine();
                            }
                            await QueueNextPlayer(inMessage, gameDoc, messageStateCollector);
                            break;

                        case Documents.BlackjackActionType.Split:
                            sb.Append("`split` not supported yet.");
                            break;

                        case Documents.BlackjackActionType.Surrender:
                            gameDoc.bets[inMessage.user_id] /= 2;
                            sb.AppendFormat("{0} surrenders ¤{1:#,#}", SR.U.IdToName[inMessage.user_id], gameDoc.bets[inMessage.user_id]);
                            gameDoc.bets[inMessage.user_id] *= -1;
                            gameDoc.users.Remove(inMessage.user_id);
                            gameDoc.user_active--;
                            await QueueNextPlayer(inMessage, gameDoc, messageStateCollector);
                            break;

                        default:
                            await messageCollector.SendMessageAsync(inMessage, "This part should not have been reached!");
                            break;
                    }
                    if (sb.Length > 0)
                        await SendMessageAsync(messageCollector, inMessage, sb.ToString());
                    gameDoc = await UpsertGameDocument(docClient, gameDoc);
                    break;


                case Messages.BlackjackMessageType.DealerPlay:
                    score = Cards.CardHelpers.GetBlackjackScore(gameDoc.hands["dealer"]);
                    sb = new StringBuilder();
                    gameDeck = gameDoc.deck;

                    sb.AppendLine("dealers's turn:");
                    AddHandToGameState(sb, gameDoc.hands["dealer"]);
                    while (score.Value < 17 || (score.Value == 17 && score.IsSoft))
                    {
                        sb.AppendLine("_hit_");
                        gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.Deal, user_id = "dealer", card = gameDeck.Deal() });
                        gameDoc.hands["dealer"].Add(gameDoc.actions.Last().card.Value);
                        AddHandToGameState(sb, gameDoc.hands["dealer"]);
                        score = Cards.CardHelpers.GetBlackjackScore(gameDoc.hands["dealer"]);
                    }
                    if (!score.IsBust)
                        sb.AppendLine("_stand_");

                    await SendMessageAsync(messageCollector, inMessage, sb.ToString());
                    gameDoc = await UpsertGameDocument(docClient, gameDoc);
                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                    goto case Messages.BlackjackMessageType.ToFinish;


                case Messages.BlackjackMessageType.ToFinish:
                    gameDoc.actions.Add(new Documents.BlackjackAction { type = Documents.BlackjackActionType.StateChange, to_state = Documents.BlackjackGameState.Finished });
                    gameDoc.state = Documents.BlackjackGameState.Finished;
                    if (gameDoc.hands.ContainsKey("dealer"))
                        dealerScore = Cards.CardHelpers.GetBlackjackScore(gameDoc.hands["dealer"]);
                    else
                        dealerScore = new Cards.CardHelpers.BlackjackScore();
                    sb = new StringBuilder();
                    var finalScores = gameDoc.hands.ToDictionary(kvp => kvp.Key, kvp => Cards.CardHelpers.GetBlackjackScore(kvp.Value));
                    await ShowGameState(messageCollector, inMessage, gameDoc, showDealer: true);

                    // Check for dealer blackjack first (because we would have short-circuited here)
                    if (dealerScore.IsBlackjack)
                    {
                        for (int i = 0; i < gameDoc.users.Count; i++)
                        {
                            var finalScore = finalScores[gameDoc.users[i]];
                            long amount = 0;
                            if (finalScore.IsBlackjack)
                            {
                                sb.AppendFormat("{0} tied (¤{1:#,#} bet returned)", SR.U.IdToName[gameDoc.users[i]], gameDoc.bets[gameDoc.users[i]]);
                                sb.AppendLine();
                            }
                            else
                            {
                                amount = gameDoc.bets[gameDoc.users[i]];
                                sb.AppendFormat("{0} loses ¤{1:#,#}", SR.U.IdToName[gameDoc.users[i]], amount);
                                sb.AppendLine();
                            }
                            await messageStateCollector.AddAsync(
                                new BrokeredMessage(
                                    new Messages.ServiceBusBlackjack
                                    {
                                        type = Messages.BlackjackMessageType.UpdateBalance,
                                        channel_id = inMessage.channel_id,
                                        thread_ts = inMessage.thread_ts,
                                        user_id = gameDoc.users[i],
                                        amount = -amount
                                    })
                                {
                                    // Schedule every 2s to give cosmos db a chance
                                    ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddSeconds(1 + 2 * i)
                                });
                        }
                    }
                    else
                    {
                        // Check if blackjack, if busted, else compare to dealer score
                        for (int i = 0; i < gameDoc.users.Count; i++)
                        {
                            var finalScore = finalScores[gameDoc.users[i]];
                            long amount = gameDoc.bets[gameDoc.users[i]];
                            if (finalScore.IsBlackjack)
                            {
                                amount = amount * 3 / 2;
                                sb.AppendFormat("{0} wins! (+ ¤{1:#,#})", SR.U.IdToName[gameDoc.users[i]], amount);
                                sb.AppendLine();
                            }
                            else if (finalScore.IsBust)
                            {
                                sb.AppendFormat("{0} loses! (- ¤{1:#,#})", SR.U.IdToName[gameDoc.users[i]], amount);
                                sb.AppendLine();
                                amount *= -1;
                            }
                            else
                            {
                                if (finalScore.Value > dealerScore.Value || (dealerScore.IsBust && !finalScore.IsBust))
                                {
                                    sb.AppendFormat("{0} wins! (+ ¤{1:#,#})", SR.U.IdToName[gameDoc.users[i]], amount);
                                    sb.AppendLine();
                                }
                                else if (finalScore.Value == dealerScore.Value)
                                {
                                    amount = 0;
                                    sb.AppendFormat("{0} ties! (¤{1:#,#} returned)", SR.U.IdToName[gameDoc.users[i]], amount);
                                    sb.AppendLine();
                                }
                                else if (finalScore.Value < dealerScore.Value)
                                {
                                    sb.AppendFormat("{0} loses! (- ¤{1:#,#})", SR.U.IdToName[gameDoc.users[i]], amount);
                                    sb.AppendLine();
                                    amount *= -1;
                                }
                            }
                            await messageStateCollector.AddAsync(
                                new BrokeredMessage(
                                    new Messages.ServiceBusBlackjack
                                    {
                                        type = Messages.BlackjackMessageType.UpdateBalance,
                                        channel_id = inMessage.channel_id,
                                        thread_ts = inMessage.thread_ts,
                                        user_id = gameDoc.users[i],
                                        amount = amount
                                    })
                                {
                                    // Schedule every 2s to give cosmos db a chance
                                    ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddSeconds(2 * (i + 1))
                                });
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                    await SendMessageAsync(messageCollector, inMessage, sb.ToString());
                    gameDoc = await UpsertGameDocument(docClient, gameDoc);
                    break;


                default:
                    await messageCollector.SendMessageAsync(inMessage, $"NOT SUPPORTED YET: {inMessage.channel_id}|{inMessage.thread_ts}");
                    break;
            }
        }

        private static async Task QueueNextPlayer(Messages.ServiceBusBlackjack inMessage, Documents.Blackjack gameDoc, IAsyncCollector<BrokeredMessage> messageStateCollector)
        {
            if (++gameDoc.user_active >= gameDoc.users.Count)
                await messageStateCollector.AddAsync(
                    new BrokeredMessage(
                        new Messages.ServiceBusBlackjack
                        {
                            type = Messages.BlackjackMessageType.DealerPlay,
                            channel_id = inMessage.channel_id,
                            thread_ts = inMessage.thread_ts
                        })
                    {
                        ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddSeconds(1)
                    });
            else
                await messageStateCollector.AddAsync(
                    new BrokeredMessage(
                        new Messages.ServiceBusBlackjack
                        {
                            type = Messages.BlackjackMessageType.GameAction,
                            channel_id = inMessage.channel_id,
                            thread_ts = inMessage.thread_ts,
                            action = Documents.BlackjackActionType.Prompt,
                            user_id = gameDoc.users[gameDoc.user_active]
                        })
                    {
                        ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddSeconds(1)
                    });
        }

        private static async Task<Documents.Blackjack> UpsertGameDocument(DocumentClient docClient, Documents.Blackjack gameDoc)
        {
            return (Documents.Blackjack)(dynamic)(await docClient.UpsertDocumentAsync(
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
                disableAutomaticIdGeneration: true)).Resource;
        }

        private static Task ShowGameState(IAsyncCollector<Slack.Events.Inner.message> messageCollector, Messages.ServiceBusBlackjack inMessage, Documents.Blackjack gameDoc, bool showDealer = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Dealt cards:");

            // dealer is not in users, so put them first
            sb.Append("*Dealer*: ");
            if (showDealer)
                AddHandToGameState(sb, gameDoc.hands["dealer"]);
            else
                AddHandToGameState(sb, new List<Cards.Cards> { gameDoc.hands["dealer"][0], Cards.Cards.Invalid });

            foreach (var user in gameDoc.users)
            {
                sb.AppendFormat("*{0}*: ", SR.U.IdToName[user]);
                AddHandToGameState(sb, gameDoc.hands[user]);
            }
            return messageCollector.SendMessageAsync(inMessage, sb.ToString());
        }

        private static void AddHandToGameState(StringBuilder sb, List<Cards.Cards> hand)
        {
            sb.Append('[');
            var score = Cards.CardHelpers.GetBlackjackScore(hand);
            if (score.IsBust)
                sb.Append("Bust(");
            if (!hand.Contains(Cards.Cards.Invalid))
                sb.Append(score.Value);
            else
                sb.Append('?');
            if (score.IsBust)
                sb.Append(')');
            sb.Append("] ");
            sb.Append(string.Join(", ", hand.Select(c => Cards.CardHelpers.ToNiceString(c))));
            sb.AppendLine();
        }
    }
}
