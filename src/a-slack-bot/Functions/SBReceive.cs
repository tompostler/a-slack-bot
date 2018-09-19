using Microsoft.Azure.WebJobs;
using System.Net.Http;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SBReceive
    {
        private static readonly HttpClient httpClient = new HttpClient();

        private static Task SendMessageAsync(this IAsyncCollector<Slack.Events.Inner.message> messageCollector, Messages.ServiceBusBlackjack metadata, string text)
        {
            return messageCollector.AddAsync(
                new Slack.Events.Inner.message
                {
                    channel = metadata.channel_id,
                    thread_ts = metadata.thread_ts,
                    text = text
                });
        }
    }
}
