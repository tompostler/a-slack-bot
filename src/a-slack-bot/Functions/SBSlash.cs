using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static class SBSlash
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly JsonSerializer jsonSerializer = new JsonSerializer();

        [FunctionName(nameof(ReceiveSlashFromServiceBus))]
        public static async Task ReceiveSlashFromServiceBus(
            [ServiceBusTrigger(Constants.SBQ.InputSlash)]BrokeredMessage slashMessage,
            ILogger logger)
        {
            // SB is faster than returning the ephemeral response, so just chill for a bit
            await Task.Delay(TimeSpan.FromSeconds(0.5));

            Messages.ServiceBusInputSlash slash = null;
            var stream = slashMessage.GetBody<Stream>();
            using (var sr = new StreamReader(stream))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                if (Settings.Debug)
                {
                    var body = await sr.ReadToEndAsync();
                    logger.LogInformation("Body: {0}", body);
                    slash = JsonConvert.DeserializeObject<Messages.ServiceBusInputSlash>(body);
                }
                else
                {
                    slash = jsonSerializer.Deserialize<Messages.ServiceBusInputSlash>(jsonTextReader);
                }
            }
            var slashData = slash.slashData;

            switch (slashData.command)
            {
                case "/spaces":
                    var text = slashData.text;
                    StringBuilder sb = new StringBuilder(text.Length * 2);
                    for (int i = 0; i < text.Length - 1; i++)
                        sb.Append(text[i]).Append(' ');
                    sb.Append(text[text.Length - 1]);
                    await SendResponse(logger, slashData, sb.ToString());
                    break;

                default:
                    await SendResponse(logger, slashData, "NOT SUPPORTED");
                    break;
            }
        }

        private static async Task SendResponse(ILogger logger, Slack.Slash slashData, string text, bool in_channel = true)
        {
            logger.LogInformation("{0}: {1} {2} {3}", slashData.response_url, text, slashData.user_id, slashData.command);
            var response = await httpClient.PostAsJsonAsync(slashData.response_url, new
            {
                response_type = in_channel ? "in_channel" : "ephemeral",
                attachments = new[]
                {
                    new
                    {
                        text,
                        footer = $"<@{slashData.user_id}>, {slashData.command}"
                    }
                }
            });
            logger.LogInformation("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
        }
    }
}
