using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static class SBSend
    {
        private static readonly HttpClient httpClient = new HttpClient();
        static SBSend()
        {
            if (!string.IsNullOrWhiteSpace(Settings.SlackOauthToken))
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.SlackOauthToken);
        }

        [FunctionName(nameof(SBSendMessage))]
        public static async Task SBSendMessage(
            [ServiceBusTrigger(C.SBQ.SendMessage)]Slack.Events.Inner.message messageData,
            ILogger logger)
        {
            var response = await httpClient.PostAsJsonAsync("https://slack.com/api/chat.postMessage", messageData);
            logger.LogInformation("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());

            var responseContent = await response.Content.ReadAsAsync<Slack.WebAPIResponse>();
            if (!responseContent.ok)
                switch (responseContent.error)
                {
                    // These are fine in this scenario
                    case "not_in_channel":
                        break;

                    case "app_rate_limited":
                        await Task.Delay(response.Headers.RetryAfter.Delta ?? TimeSpan.FromSeconds(5));
                        throw new Exception("Retry-After; requeue with ServiceBus");

                    default:
                        throw new Exception("Slack API Error: " + responseContent.error);
                }
        }
    }
}
