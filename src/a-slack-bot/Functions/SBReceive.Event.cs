using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SBReceive
    {
        [FunctionName(nameof(SBReceiveEvent))]
        public static async Task SBReceiveEvent(
            [ServiceBusTrigger(C.SBQ.InputEvent)]Messages.ServiceBusInputEvent eventMessage,
            [DocumentDB(C.CDB.DN, C.CDB.CN, ConnectionStringSetting = C.CDB.CSS, PartitionKey = C.CDB.P, CreateIfNotExists = true)]IAsyncCollector<Documents.Event> documentCollector,
            [ServiceBus(C.SBQ.SendMessage)]IAsyncCollector<Slack.Events.Inner.message> messageCollector,
            [ServiceBus(C.SBQ.InputThread)]IAsyncCollector<Slack.Events.Inner.message> messageThreadCollector,
            ILogger logger)
        {
            if (Settings.Debug)
                logger.LogInformation("Msg: {0}", JsonConvert.SerializeObject(eventMessage));

            // First, send it to cosmos for the records
            var document = new Documents.Event { Content = eventMessage.@event };
            await documentCollector.AddAsync(document);
            if (Settings.Debug)
                logger.LogInformation("Doc: {0}", JsonConvert.SerializeObject(document));

            // Then, do something with it
            switch (eventMessage.@event)
            {
                case Slack.Events.Inner.app_mention app_mention:
                    await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = app_mention.channel, text = $"ACK <@{app_mention.user}>", thread_ts = app_mention.thread_ts });
                    break;

                case Slack.Events.Inner.message message:
                    if (string.IsNullOrWhiteSpace(message.thread_ts))
                        await messageThreadCollector.AddAsync(message);
                    break;

                default:
                    logger.LogInformation("Event type '{0}' is not yet supported.", eventMessage.@event.type);
                    break;
            }
        }
    }
}
