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

            // Check for optimized case of only one response, but still get and upsert the doc to keep track of the count
            // TODO: Consider a sproc for this operation
            if (SR.R.AllResponses[matchedKey].Count == 1)
            {
                async Task upsertWithCountIncreased()
                {
                    var sracr = SR.R.AllResponses[matchedKey].Single();
                    var doc = (await docClient.ReadDocumentAsync<Documents2.Response>(
                        UriFactory.CreateDocumentUri(C.CDB2.DN, C.CDB2.Col.CustomResponses, sracr.Key),
                        new RequestOptions { PartitionKey = new PartitionKey(matchedKey) })).Document;
                    doc.count++;
                    await docClient.UpsertDocumentAsync(
                        C.CDB2.CUs[C.CDB2.Col.CustomResponses],
                        doc,
                        new RequestOptions { PartitionKey = new PartitionKey(matchedKey) },
                        disableAutomaticIdGeneration: true);
                }
                await Task.WhenAll(new[] {
                    messageCollector.AddAsync(new Slack.Events.Inner.message { channel = message.channel, text = SR.R.AllResponses[matchedKey].Single().Value }),
                    upsertWithCountIncreased()
                });
                return;
            }

            // Get the minimum display count
            var count = docClient.CreateDocumentQuery<int>(
                C.CDB2.CUs[C.CDB2.Col.CustomResponses],
                $"SELECT VALUE MIN(r.{nameof(Documents2.Response.count)}) FROM r",
                new FeedOptions { PartitionKey = new PartitionKey(matchedKey) })
                .AsEnumerable().FirstOrDefault();

            // Pick one
            var response = docClient.CreateDocumentQuery<Documents2.Response>(
                C.CDB2.CUs[C.CDB2.Col.CustomResponses],
                $"SELECT TOP 1 * FROM r WHERE r.{nameof(Documents2.Response.count)} = {count} ORDER BY r.{nameof(Documents2.Response.random)}",
                new FeedOptions { PartitionKey = new PartitionKey(matchedKey) })
                .AsEnumerable().FirstOrDefault();

            response.count++;
            response.random = Guid.NewGuid();

            // Send the message and upsert the used ids doc
            await Task.WhenAll(new[]
            {
                docClient.UpsertDocumentAsync(
                    C.CDB2.CUs[C.CDB2.Col.CustomResponses],
                    response,
                    new RequestOptions { PartitionKey = new PartitionKey(matchedKey) },
                    disableAutomaticIdGeneration: true),
                messageCollector.AddAsync(new Slack.Events.Inner.message{channel = message.channel, text = response.value})
            });
        }
    }
}
