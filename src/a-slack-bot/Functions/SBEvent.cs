using a_slack_bot.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static class SBEvent
    {
        private static readonly HttpClient httpClient = new HttpClient();

        [FunctionName(nameof(SBReceiveEvent))]
        public static async Task SBReceiveEvent(
            [ServiceBusTrigger(C.SBQ.InputEvent)]Messages.ServiceBusInputEvent eventMessage,
            [DocumentDB(C.CDB.DN, C.CDB.CN, ConnectionStringSetting = C.CDB.CSS, PartitionKey = C.CDB.P, CreateIfNotExists = true)]IAsyncCollector<Documents.Event> documentCollector,
            [ServiceBus(C.SBQ.SendMessage)]IAsyncCollector<Slack.Events.Inner.message> messageCollector,
            ILogger logger)
        {
            if (Settings.Debug)
                logger.LogInformation("Msg: {0}", JsonConvert.SerializeObject(eventMessage));

            // First, send it to cosmos for the records
            var document = eventMessage.@event.ToDoc();
            await documentCollector.AddAsync(document);
            if (Settings.Debug)
                logger.LogInformation("Doc: {0}", JsonConvert.SerializeObject(document));

            // Then, do something with it
        }
    }
}
