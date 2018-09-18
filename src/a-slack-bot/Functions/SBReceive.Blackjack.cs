using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System.Net;
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

            // Handle a balance request
            if (inMessage.type == Messages.BlackjackMessageType.GetBalances)
            {
                try
                {
                    var gameBalancesDoc = await docClient.ReadDocumentAsync<Documents.Blackjack>(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, nameof(Documents.BlackjackStandings)), new RequestOptions { PartitionKey = new PartitionKey("Game|" + nameof(Documents.Blackjack)) });
                    await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = inMessage.channel_id, thread_ts = inMessage.thread_ts, text = $"another not supported spot yet: {inMessage.channel_id}|{inMessage.thread_ts}" });

                }
                catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
                {
                    await docClient.CreateDocumentAsync(
                        UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                        new Documents.BlackjackStandings { Content = new System.Collections.Generic.Dictionary<string, ulong>() },
                        new RequestOptions { PartitionKey = new PartitionKey("Game|" + nameof(Documents.Blackjack)) });

                    // Let SB retry us. Should only ever hit this once.
                    throw;
                }
            }

            // Get the game doc
            await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = inMessage.channel_id, thread_ts = inMessage.thread_ts, text = $"another not supported spot yet 2: {inMessage.channel_id}|{inMessage.thread_ts}" });
            var gameDoc = await docClient.ReadDocumentAsync<Documents.Blackjack>(
                UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, $"{inMessage.channel_id}|{inMessage.thread_ts}"),
                new RequestOptions { PartitionKey = new PartitionKey("Game|" + nameof(Documents.Blackjack)) });
            logger.LogInformation("Got game doc");

            switch (inMessage.type)
            {
                case Messages.BlackjackMessageType.Timer_StartGame:
                    break;
        }
        }
    }
}
