using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static class HttpEntry
    {
        [FunctionName(nameof(NotifyReleaseVersion))]
        public static async Task<HttpResponseMessage> NotifyReleaseVersion(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "internal/notify/releaseversion")]HttpRequestMessage req,
            [ServiceBus(Constants.SBQ.SendMessage)]IAsyncCollector<Slack.Events.Inner.message> messageCollector,
            ILogger logger)
        {
            if (Settings.Debug)
                logger.LogInformation("Body: {0}", await req.Content.ReadAsStringAsync());

            if (string.IsNullOrWhiteSpace(Settings.Runtime.NotifyChannelId))
            {
                logger.LogInformation("No notification channel set.");
                return req.CreateResponse(HttpStatusCode.OK);
            }

            // Get stuff from the message
            var version = await req.Content.ReadAsStringAsync();

            // Send it off to be processed
            logger.LogInformation("Sending inner event into the queue.");
            await messageCollector.AddAsync(new Slack.Events.Inner.message
            {
                channel = Settings.Runtime.NotifyChannelId,
                text = $"Released:\n```{typeof(SlackEntry).Assembly.ManifestModule.Name} v{GitVersionInformation.SemVer}```"
            });

            // Return all is well
            return req.CreateResponse(HttpStatusCode.OK);
        }

    }
}
