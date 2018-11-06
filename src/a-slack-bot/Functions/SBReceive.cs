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
            // Just take whichever queue is larger
            var queuesToLookAt = await Task.WhenAll(new[]
            {
                sbNamespace.GetQueueAsync(C.SBQ.SendMessage),
                sbNamespace.GetQueueAsync(C.SBQ.SendMessageEphemeral)
            });
            var maxLen = Math.Max(queuesToLookAt[0].MessageCount, queuesToLookAt[1].MessageCount);
            await messageCollector.AddAsync(
                new BrokeredMessage(message)
                {
                    ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddSeconds(maxLen / 2.0)
                });
            await messageCollector.FlushAsync();
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

        private static Task AddEAsync(this IAsyncCollector<BrokeredMessage> messageCollector, Slack.Slash metadata, string text)
        {
            return messageCollector.AddAsync(
                new Slack.Events.Inner.message
                {
                    channel = metadata.channel_id,
                    text = text,
                    user = metadata.user_id
                });
        }
    }
}
