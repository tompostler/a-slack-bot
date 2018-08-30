using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
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
            [DocumentDB(C.CDB.DN, C.CDB.CN, ConnectionStringSetting = C.CDB.CSS, PartitionKey = C.CDB.P, CreateIfNotExists = true)]IAsyncCollector<Documents.OAuthToken> documentCollector,
            ILogger logger)
        {
            await Task.WhenAll(new[]
            {
                SR.Init(logger),
                // SB is faster than returning the ephemeral response, so just chill for a bit
                Task.Delay(TimeSpan.FromSeconds(0.5))
            });

            var slashData = slashMessage.slashData;

            // We need to decide if we should post as the user or as ourselves
            string userToken = null;
            if (SR.T.ChatWriteUser.ContainsKey(slashData.user_id))
                userToken = SR.T.ChatWriteUser[slashData.user_id];

            switch (slashData.command)
            {
                case "/asb-send-as-me":
                    // TODO: https://api.slack.com/docs/oauth
                    //await documentCollector.AddAsync(new Documents.OAuthToken { token_type = "chat:write:user", Id = slashData.user_id });
                    await SendResponse(logger, slashData, ":construction: under construction :construction:", in_channel: false);
                    break;

                case "/disapprove":
                    await SendResponse(logger, slashData, "ಠ_ಠ");
                    break;

                case "/flip":
                    await SendResponse(logger, slashData, slashData.text + " (╯°□°)╯︵ ┻━┻");
                    break;

                case "/spaces":
                    var text = slashData.text;
                    StringBuilder sb = new StringBuilder(text.Length * 2);
                    var enumerator = StringInfo.GetTextElementEnumerator(text);
                    while (enumerator.MoveNext())
                        sb.Append(enumerator.GetTextElement()).Append(' ');
                    await SendResponse(logger, slashData, sb.ToString());
                    break;

                default:
                    await SendResponse(logger, slashData, "*NOT SUPPORTED*", in_channel: false);
                    break;
            }
        }

        private static async Task SendResponse(ILogger logger, Slack.Slash slashData, string text, bool in_channel = true)
        {
            logger.LogInformation("{0}: {1} {2} {3}", slashData.response_url, text, slashData.user_id, slashData.command);
            var response = await httpClient.PostAsJsonAsync(slashData.response_url, new
            {
                response_type = in_channel ? "in_channel" : "ephemeral",
                text,
                attachments = new[]
                {
                    new
                    {
                        text = string.Empty,
                        footer = $"<@{slashData.user_id}>, {slashData.command}"
                    }
                }
            });
            logger.LogInformation("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
        }
    }
}
