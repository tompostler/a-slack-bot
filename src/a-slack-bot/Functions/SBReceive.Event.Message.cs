using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
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
            var text = message.text;
            var textD = SBReceiveEventMessageDecode(text);
            var textDL = textD.ToLower();
            logger.LogInformation("{0}: {1}", nameof(textDL), textDL);

            await SBReceiveEventMessageCustomResponse(message, textDL, docClient, messageCollector, logger);
        }

        private static Regex ConversationId = new Regex(@"<#(?<id>\w+)(?>\|[a-z0-9_-]+)?>", RegexOptions.Compiled);
        private static Regex UserId = new Regex(@"<@(?<id>\w+)(?>\|[\w_-]+)?>", RegexOptions.Compiled);
        private static string SBReceiveEventMessageDecode(string messageText)
        {
            var matches = ConversationId.Matches(messageText);
            for (int i = 0; i < matches.Count; i++)
                if (SR.C.IdToName.ContainsKey(matches[i].Groups["id"].Value))
                    messageText = messageText.Replace(matches[i].Value, '#' + SR.C.IdToName[matches[i].Groups["id"].Value]);
            matches = UserId.Matches(messageText);
            for (int i = 0; i < matches.Count; i++)
                if (SR.U.IdToName.ContainsKey(matches[i].Groups["id"].Value))
                    messageText = messageText.Replace(matches[i].Value, '@' + SR.U.IdToName[matches[i].Groups["id"].Value]);
            // Remove any remaining special characters (URLs, etc)
            messageText = messageText.Replace("<", string.Empty).Replace(">", string.Empty);
            // Put back the escaped chars
            messageText = messageText.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");

            return messageText;
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

            // Get the used keys dictionary
            Documents.ResponsesUsed usedIds = null;
            try
            {
                usedIds = await docClient.ReadDocumentAsync<Documents.ResponsesUsed>(
                    UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, nameof(Documents.ResponsesUsed)),
                    new RequestOptions { PartitionKey = new PartitionKey(nameof(Documents.Response) + "|" + matchedKey) });

                if (usedIds.Content.Count >= SR.R.AllResponses[matchedKey].Count)
                {
                    logger.LogInformation("Used IDs count indicates all used. Reseting.");
                    usedIds = new Documents.ResponsesUsed { Subtype = matchedKey };
                }
            }
            catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogWarning("Didn't find a response used document. Creating one.", matchedKey);
                usedIds = new Documents.ResponsesUsed { Subtype = matchedKey };
            }
            usedIds.Content = usedIds.Content ?? new HashSet<string>();

            // Pick one to respond with
            var unusedIds = SR.R.AllResponses[matchedKey].Keys.Except(usedIds.Content).ToList();
            var pickedId = unusedIds[SR.Rand.Next(unusedIds.Count)];
            usedIds.Content.Add(pickedId);
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
