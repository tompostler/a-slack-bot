using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
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
                    if (slashData.text == "help" || string.IsNullOrWhiteSpace(slashData.text))
                        await SendResponse(logger, slashData, "Visit https://api.slack.com/custom-integrations/legacy-tokens to generate a token, or send `clear` to remove your existing token.", userToken, in_channel: false);
                    else if (slashData.text == "clear")
                    {
                        await documentCollector.AddAsync(new Documents.OAuthToken { token_type = "user", Id = slashData.user_id, Content = string.Empty });
                        await SendResponse(logger, slashData, ":thumbsup:", userToken, in_channel: false);
                        SR.Initialized = false;
                    }
                    else
                    {
                        await documentCollector.AddAsync(new Documents.OAuthToken { token_type = "user", Id = slashData.user_id, Content = slashData.text });
                        await SendResponse(logger, slashData, ":thumbsup:", userToken, in_channel: false);
                    }
                    break;

                case "/disapprove":
                    await SendResponse(logger, slashData, "ಠ_ಠ", userToken);
                    break;

                case "/flip":
                    await SendResponse(logger, slashData, slashData.text + " (╯°□°)╯︵ ┻━┻", userToken);
                    break;

                case "/spaces":
                    var text = slashData.text;
                    StringBuilder sb = new StringBuilder(text.Length * 2);
                    var enumerator = StringInfo.GetTextElementEnumerator(text);
                    while (enumerator.MoveNext())
                        sb.Append(enumerator.GetTextElement()).Append(' ');
                    await SendResponse(logger, slashData, sb.ToString(), userToken);
                    break;

                default:
                    await SendResponse(logger, slashData, "*NOT SUPPORTED*", userToken, in_channel: false);
                    break;
            }
        }

        private static async Task SendResponse(ILogger logger, Slack.Slash slashData, string text, string userToken, bool in_channel = true)
        {
            logger.LogInformation("{0}: {1} {2} {3}", slashData.response_url, text, slashData.user_id, slashData.command);

            object payload = null;
            if (in_channel)
                payload = new
                {
                    response_type = "in_channel",
                    text,
                    attachments = new[]
                    {
                        new
                        {
                            text = string.Empty,
                            footer = $"<@{slashData.user_id}>, {slashData.command}"
                        }
                    }
                };
            else
                payload = new
                {
                    response_type = "ephemeral",
                    text
                };

            if (string.IsNullOrWhiteSpace(userToken) || !in_channel)
            {
                var response = await httpClient.PostAsJsonAsync(slashData.response_url, payload);
                logger.LogInformation("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            else
            {
                // Post as the user
                var msg = new HttpRequestMessage(HttpMethod.Post, "https://slack.com/api/chat.postMessage")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(new Slack.Events.Inner.message
                    {
                        as_user = true,
                        channel = slashData.channel_id,
                        text = text
                    }), Encoding.UTF8, "application/json")
                };
                msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
                var response = await httpClient.SendAsync(msg);
                var responseObj = await response.Content.ReadAsAsync<Slack.WebAPIResponse>();
                if (!responseObj.ok)
                {
                    if (responseObj.error == "invalid_auth" || responseObj.error == "token_revoked")
                    {
                        response = await httpClient.PostAsJsonAsync(slashData.response_url, new { response_type = "ephemeral", text = "Your user token is invalid." });
                        logger.LogWarning("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
                    }
                    else
                    {
                        response = await httpClient.PostAsJsonAsync(slashData.response_url, new { response_type = "ephemeral", text = $"Something went wrong: `{responseObj.error}`" });
                        logger.LogError("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
                    }
                }
            }
        }
    }
}
