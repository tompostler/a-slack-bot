using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SBReceive
    {
        public static async Task SBReceiveEventMessage(
            Slack.Events.Inner.message message,
            DocumentClient docClient,
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

            await SBReceiveEventMessageCustomResponse(message, textDL, docClient, messageCollector, logger);
        }

        private static async Task SBReceiveEventMessageCustomResponse(
            Slack.Events.Inner.message message,
            string textDL,
            DocumentClient docClient,
            IAsyncCollector<Slack.Events.Inner.message> messageCollector,
            ILogger logger)
        {
            await SR.Init(logger);

            if (message.bot_id == SR.U.BotUser.profile.bot_id)
            {
                logger.LogInformation("Detected message from self. Not responding.");
                return;
            }

            string matchedKey = null;
            foreach (var key in SR.R.Keys)
                if (textDL.Contains(key))
                {
                    matchedKey = key;
                    break;
                }

            if (matchedKey == null)
                return;

            logger.LogInformation("Found a custom response match with key '{0}'", matchedKey);

            // Check for optimized case of only one response
            if (SR.R.AllResponses[matchedKey].Count == 1)
            {
                await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = message.channel, text = SR.R.AllResponses[matchedKey].Single().Value });
                return;
            }

            // Get the minimum display count
            var count = docClient.CreateDocumentQuery<int>(
                UriFactory.CreateDocumentCollectionUri(C.CDB2.DN, C.CDB2.Col.CustomResponses),
                $"SELECT VALUE MIN(r.{nameof(Documents2.Response.count)}) FROM r",
                new FeedOptions { PartitionKey = new PartitionKey(matchedKey) })
                .AsEnumerable().FirstOrDefault();

            // Pick one
            var response = docClient.CreateDocumentQuery<Documents2.Response>(
                UriFactory.CreateDocumentCollectionUri(C.CDB2.DN, C.CDB2.Col.CustomResponses),
                $"SELECT TOP 1 * FROM r WHERE c.{nameof(Documents2.Response.count)} = {count} ORDER BY r.{nameof(Documents2.Response.random)}",
                new FeedOptions { PartitionKey = new PartitionKey(matchedKey) })
                .AsEnumerable().FirstOrDefault();

            response.count++;
            response.random = Guid.NewGuid().ToString();

            // Send the message and upsert the used ids doc
            await Task.WhenAll(new[]
            {
                docClient.UpsertDocumentAsync(
                    UriFactory.CreateDocumentCollectionUri(C.CDB2.DN, C.CDB2.Col.CustomResponses),
                    response,
                    new RequestOptions { PartitionKey = new PartitionKey(matchedKey) },
                    disableAutomaticIdGeneration: true),
                messageCollector.AddAsync(new Slack.Events.Inner.message{channel = message.channel, text = response.value})
            });
        }
    }
}
