using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static class SBOAuth
    {
        private static readonly HttpClient httpClient = new HttpClient();
        static SBOAuth()
        {
            if (!string.IsNullOrWhiteSpace(Settings.SlackOauthToken))
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.SlackOauthToken);
        }

        [FunctionName(nameof(SBOAuthMessage))]
        public static async Task SBOAuthMessage(
            [ServiceBusTrigger(C.SBQ.OAuth)]Messages.ServiceBusOAuth messageData,
            ILogger logger)
        {
            var response = await httpClient.PostAsFormDataAsync("https://slack.com/api/oauth.access", new Dictionary<string, string>
            {
                ["client_id"] = Settings.SlackClientID,
                ["client_secret"] = Settings.SlackClientSecret,
                ["code"] = messageData.code
            });
            logger.LogInformation("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());

            // TODO: record this in the database.
        }
    }
}
