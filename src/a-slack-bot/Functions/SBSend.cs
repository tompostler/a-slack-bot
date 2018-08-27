using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static class SBSend
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly JsonSerializer jsonSerializer = new JsonSerializer();

        static SBSend()
        {
            if (!string.IsNullOrWhiteSpace(Settings.SlackOauthBotToken))
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.SlackOauthBotToken);
        }

        [FunctionName(nameof(ReceiveSendMEssageFromServiceBus))]
        public static async Task ReceiveSendMEssageFromServiceBus(
            [ServiceBusTrigger(Constants.SBQ.SendMessage)]BrokeredMessage slashMessage,
            ILogger logger)
        {
            Slack.Events.Inner.message messageData = null;
            var stream = slashMessage.GetBody<Stream>();
            using (var sr = new StreamReader(stream))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                if (Settings.Debug)
                {
                    var body = await sr.ReadToEndAsync();
                    logger.LogInformation("Body: {0}", body);
                    messageData = JsonConvert.DeserializeObject<Slack.Events.Inner.message>(body);
                }
                else
                {
                    messageData = jsonSerializer.Deserialize<Slack.Events.Inner.message>(jsonTextReader);
                }
            }

            await SendResponse(logger, messageData);
        }

        private static async Task SendResponse(ILogger logger, Slack.Events.Inner.message messageData)
        {
            var response = await httpClient.PostAsJsonAsync("https://slack.com/api/chat.postMessage", messageData);
            logger.LogInformation("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
        }
    }
}
