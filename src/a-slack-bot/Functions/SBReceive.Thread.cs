﻿using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SBReceive
    {
        [FunctionName(nameof(SBReceiveThread))]
        public static async Task SBReceiveThread(
            [ServiceBusTrigger(C.SBQ.InputThread)]Slack.Events.Inner.message eventMessage,
            [ServiceBus(C.SBQ.Blackjack)]IAsyncCollector<Messages.Blackjack> messageCollector,
            [DocumentDB(ConnectionStringSetting = C.CDB.CSS)]DocumentClient docClient,
            ILogger logger)
        {
            if (Settings.Debug)
                logger.LogInformation("Msg: {0}", JsonConvert.SerializeObject(eventMessage));

            // If it's not a user message, then ignore it
            if (string.IsNullOrEmpty(eventMessage.user))
            {
                logger.LogInformation("Ignoring non-user message.");
                return;
            }

            // First, see if there's any matching documents
            Documents.Blackjack gameDoc;
            try
            {
                gameDoc = await docClient.ReadDocumentAsync<Documents.Blackjack>(
                    UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, eventMessage.thread_ts),
                    new RequestOptions { PartitionKey = new Documents.Blackjack().PK });
                logger.LogInformation("Got game doc");
            }
            catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("No game doc");
                return;
            }

            // At this point, we've got a game
            await SBReceiveThreadBlackjack(docClient, gameDoc, eventMessage, messageCollector, logger);
        }
    }
}
