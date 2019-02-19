using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SBReceive
    {
        public static async Task SBReceiveEventMessage(
            Slack.Events.Inner.message message,
            DocumentClient docClient,
            IAsyncCollector<Messages.ReactionAdd> reactionCollector,
            IAsyncCollector<Slack.Events.Inner.message> messageCollector,
            ILogger logger)
        {
            await SR.Init(logger);
            if (String.IsNullOrEmpty(message.text))
            {
                logger.LogInformation("Empty message text. Exiting from {0}.", nameof(SBReceiveEventMessage));
                return;
            }

            var text = message.text;
            var textD = SR.MessageToPlainText(text);
            var textDL = textD.ToLower();
            logger.LogInformation("{0}: {1}", nameof(textDL), textDL);

            try
            {
                await Task.WhenAll(new[] {
                    SBReceiveEventMessageCustomReThing<Documents.Reaction>(message, textDL, SR.Ra.AllReactions, docClient, reactionCollector, messageCollector, logger),
                    SBReceiveEventMessageCustomReThing<Documents.Response>(message, textDL, SR.Re.AllResponses, docClient, reactionCollector, messageCollector, logger),
                    SBReceiveEventMessageRelativeDateTime(message, textDL, messageCollector, logger)
                });
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to do something, but don't want to try again: {0}", ex.ToString());
            }
        }

        private static async Task SBReceiveEventMessageRelativeDateTime(
            Slack.Events.Inner.message message,
            string textDL,
            IAsyncCollector<Slack.Events.Inner.message> messageCollector,
            ILogger logger)
        {
            // Check for brackets, possibly implying a datetime to parse
            int bis = textDL.IndexOf('[');
            if (bis >= 0 && textDL.IndexOf(']', bis) >= 1)
            {
                int bie = textDL.IndexOf(']', bis);
                while (bis >= 0 && bie >= 1)
                {
                    string dateToParse = textDL.Substring(bis + 1, bie - bis - 1);
                    string parsed = RelativeDateTimeParsing.ToHumanReadble(dateToParse);
                    if (!string.IsNullOrWhiteSpace(parsed))
                        await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = message.channel, thread_ts = message.thread_ts, text = parsed });

                    bis = textDL.IndexOf('[', bie);
                    if (bis >= 0)
                        bie = textDL.IndexOf(']', bis);
                }
            }
        }

        private static async Task SBReceiveEventMessageCustomReThing<T>(
            Slack.Events.Inner.message message,
            string textDL,
            Dictionary<string, Dictionary<string, string>> SrPiece,
            DocumentClient docClient,
            IAsyncCollector<Messages.ReactionAdd> reactionCollector,
            IAsyncCollector<Slack.Events.Inner.message> messageCollector,
            ILogger logger)
            where T : Documents.ReThings, new()
        {
            if (message.bot_id == SR.U.BotUser.profile.bot_id)
            {
                logger.LogInformation("Detected message from self. Not responding.");
                return;
            }

            string matchedKey = null;
            foreach (var key in SrPiece.Keys)
                if (textDL.Contains(key))
                {
                    matchedKey = key;
                    break;
                }

            if (matchedKey == null)
                return;

            logger.LogInformation("Found a custom {0} match with key '{1}'", typeof(T).Name, matchedKey);
            var pk = new PartitionKey($"{typeof(T).Name}|{matchedKey}");

            // Check for optimized case of only one re*, but still get and upsert the doc to keep track of the count
            // TODO: Consider a sproc for this operation
            if (SrPiece[matchedKey].Count == 1)
            {
                await Task.WhenAll(new[] {
                    SBReceiveEventMessageCustomReThingSend<T>(message, SrPiece[matchedKey].Single().Value, reactionCollector, messageCollector),
                    docClient.ExecuteStoredProcedureAsync<T>(
                        UriFactory.CreateStoredProcedureUri(C.CDB.DN, C.CDB.CN, C.CDB.SP.rething_count2),
                        new RequestOptions { PartitionKey = pk },
                        SrPiece[matchedKey].Single().Key)
                });
                return;
            }

            // Get the minimum display count
            var count = docClient.CreateDocumentQuery<int>(
                C.CDB.DCUri,
                $"SELECT VALUE MIN(r.{nameof(Documents.ReThings.count)}) FROM r",
                new FeedOptions { PartitionKey = pk })
                .AsEnumerable().FirstOrDefault();

            // Pick one
            var rething = docClient.CreateDocumentQuery<T>(
                C.CDB.DCUri,
                $"SELECT TOP 1 * FROM r WHERE r.{nameof(Documents.ReThings.count)} = {count} ORDER BY r.{nameof(Documents.ReThings.random)}",
                new FeedOptions { PartitionKey = pk })
                .AsEnumerable().FirstOrDefault();

            rething.count++;
            rething.random = Guid.NewGuid();

            // Send the message and upsert the used ids doc
            await Task.WhenAll(new[]
            {
                docClient.UpsertDocumentAsync(
                    C.CDB.DCUri,
                    rething,
                    new RequestOptions { PartitionKey = pk },
                    disableAutomaticIdGeneration: true),
                SBReceiveEventMessageCustomReThingSend<T>(message, rething.value, reactionCollector, messageCollector)
            });
        }

        private static Task SBReceiveEventMessageCustomReThingSend<T>(
            Slack.Events.Inner.message message,
            string value,
            IAsyncCollector<Messages.ReactionAdd> reactionCollector,
            IAsyncCollector<Slack.Events.Inner.message> messageCollector)
            where T : Documents.ReThings, new()
        {
            if (typeof(T) == typeof(Documents.Reaction))
                return reactionCollector.AddAsync(new Messages.ReactionAdd { name = value, channel = message.channel, timestamp = message.ts });
            else if (typeof(T) == typeof(Documents.Response))
                return messageCollector.AddAsync(new Slack.Events.Inner.message { channel = message.channel, thread_ts = message.thread_ts, text = value });
            else
                return Task.CompletedTask;
        }
    }
}
