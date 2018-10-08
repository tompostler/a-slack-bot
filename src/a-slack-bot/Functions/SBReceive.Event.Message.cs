using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
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
        }
    }
}
