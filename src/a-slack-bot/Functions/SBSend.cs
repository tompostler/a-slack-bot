using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
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
            if (!string.IsNullOrWhiteSpace(Settings.SlackOauthBotToken))
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.SlackOauthBotToken);
        }

        [FunctionName(nameof(SBSendMessage))]
        public static async Task SBSendMessage(
            [ServiceBusTrigger(C.SBQ.SendMessage)]Slack.Events.Inner.message messageData,
            ILogger logger)
        {
            var response = await httpClient.PostAsJsonAsync("https://slack.com/api/chat.postMessage", messageData);
            logger.LogInformation("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
        }
    }
}
