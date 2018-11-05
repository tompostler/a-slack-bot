using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
            await SBReceiveEventMessageCustomResponse(message, docClient, messageCollector, logger);
        }

        private static async Task SBReceiveEventMessageCustomResponse(
            Slack.Events.Inner.message message,
            DocumentClient docClient,
            IAsyncCollector<Slack.Events.Inner.message> messageCollector,
            ILogger logger)
        {
            await SR.Init(logger);

            if (message.user == SR.U.BotUser.id)
            {
                logger.LogInformation("Detected message from self. Not responding.");
                return;
            }

            string messageText = message.text.Trim().ToLower();
            string matchedKey = null;
            foreach (var key in SR.R.Keys)
                if (messageText.Contains(key))
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

            // Get the used keys dictionary
            Documents.ResponsesUsed usedIds = null;
            try
            {
                usedIds = await docClient.ReadDocumentAsync<Documents.ResponsesUsed>(
                    UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, nameof(Documents.ResponsesUsed)),
                    new RequestOptions { PartitionKey = new PartitionKey(nameof(Documents.Response) + "|" + matchedKey) });

                if (usedIds.ids_used.Count >= SR.R.AllResponses[matchedKey].Count)
                {
                    logger.LogInformation("Used IDs count indicates all used. Reseting.");
                    usedIds = new Documents.ResponsesUsed { key = matchedKey };
                }
            }
            catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogWarning("Didn't find a response used document. Creating one.", matchedKey);
                usedIds = new Documents.ResponsesUsed { key = matchedKey };
            }
            usedIds.ids_used = usedIds.ids_used ?? new HashSet<string>();

            // Pick one to respond with
            var unusedIds = SR.R.AllResponses[matchedKey].Keys.Except(usedIds.ids_used).ToList();
            var pickedId = unusedIds[SR.Rand.Next(unusedIds.Count)];
            usedIds.ids_used.Add(pickedId);
            logger.LogInformation("Picked {0}", pickedId);

            // Send the message and upsert the used ids doc
            await Task.WhenAll(new[]
            {
                docClient.UpsertDocumentAsync(
                    UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                    usedIds,
                    new RequestOptions { PartitionKey = new PartitionKey(nameof(Documents.Response) + "|" + matchedKey) },
                    disableAutomaticIdGeneration: true),
                messageCollector.AddAsync(new Slack.Events.Inner.message{channel = message.channel, text = SR.R.AllResponses[matchedKey][pickedId]})
            });
        }
    }
}
