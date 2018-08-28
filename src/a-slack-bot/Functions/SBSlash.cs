using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static class SBSlash
    {
        private static readonly HttpClient httpClient = new HttpClient();

        [FunctionName(nameof(SBReceiveSlash))]
        public static async Task SBReceiveSlash(
            [ServiceBusTrigger(C.SBQ.InputSlash)]Messages.ServiceBusInputSlash slashMessage,
            ILogger logger)
        {
            // SB is faster than returning the ephemeral response, so just chill for a bit
            await Task.Delay(TimeSpan.FromSeconds(0.5));

            var slashData = slashMessage.slashData;

            switch (slashData.command)
            {
                case "/spaces":
                    var text = slashData.text;
                    StringBuilder sb = new StringBuilder(text.Length * 2);
                    var enumerator = StringInfo.GetTextElementEnumerator(text);
                    while (enumerator.MoveNext())
                        sb.Append(enumerator.GetTextElement()).Append(' ');
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
