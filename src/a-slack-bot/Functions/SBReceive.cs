using Microsoft.Azure.WebJobs;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SBReceive
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly NamespaceManager sbNamespace = NamespaceManager.CreateFromConnectionString(Settings.AzureWebJobsServiceBus);

        private static async Task AddAsync(this IAsyncCollector<BrokeredMessage> messageCollector, Slack.Events.Inner.message message)
        {
            var msgQueue = await sbNamespace.GetQueueAsync(C.SBQ.SendMessage);
            await messageCollector.AddAsync(
                new BrokeredMessage(message)
                {
                    ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddSeconds(msgQueue.MessageCount / 2.0)
                });
        }

        private static Task AddAsync(this IAsyncCollector<BrokeredMessage> messageCollector, Messages.ServiceBusBlackjack metadata, string text)
        {
            return messageCollector.AddAsync(
                new Slack.Events.Inner.message
                {
                    channel = metadata.channel_id,
                    thread_ts = metadata.thread_ts,
                    text = text
                });
        }

        private static Task AddAsync(this IAsyncCollector<BrokeredMessage> messageCollector, Slack.Slash metadata, string text)
        {
            return messageCollector.AddAsync(
                new Slack.Events.Inner.message
                {
                    channel = metadata.channel_id,
                    text = text
                });
        }
    }
}
