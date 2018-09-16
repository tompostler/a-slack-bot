using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static class SBSlash
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly HashSet<string> WhitelistableCommands = new HashSet<string>
        {
            "/blackjack"
        };

        [FunctionName(nameof(SBReceiveSlash))]
        public static async Task SBReceiveSlash(
            [ServiceBusTrigger(C.SBQ.InputSlash)]Messages.ServiceBusInputSlash slashMessage,
            [DocumentDB(C.CDB.DN, C.CDB.CN, ConnectionStringSetting = C.CDB.CSS, PartitionKey = C.CDB.P, CreateIfNotExists = true)]IAsyncCollector<Resource> documentCollector,
            [DocumentDB(ConnectionStringSetting = C.CDB.CSS)]DocumentClient docClient,
            ILogger logger)
        {
            await SR.Init(logger);

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
                        await docClient.DeleteDocumentAsync(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, slashData.user_id), new RequestOptions { PartitionKey = new PartitionKey(nameof(Documents.OAuthToken) + "|user") });
                        await SendResponse(logger, slashData, "token cleared :thumbsup:", in_channel: false);
                        SR.Deit();
                    }
                    else
                    {
                        await documentCollector.AddAsync(new Documents.OAuthToken { Subtype = "user", Id = slashData.user_id, token = slashData.text });
                        await SendResponse(logger, slashData, "token added :thumbsup:", in_channel: false);
                        SR.Deit();
                    }
                    break;

                case "/asb-whitelist":
                    await HandleAsbWhitelistCommand(slashData, documentCollector, docClient, logger);
                    break;

                case "/blackjack":
                    if (SR.W.CommandsChannels.ContainsKey("blackjack") && !SR.W.CommandsChannels["blackjack"].Contains(slashData.channel_id))
                        await SendResponse(logger, slashData, $"`{slashData.command}` is not whitelisted for this channel. See `/asb-whitelist` to add it.", in_channel: false);
                    else
                        await SendResponse(logger, slashData, "*NOT YET SUPPORTED*");
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
                    await SendResponse(logger, slashData, "*NOT SUPPORTED*", in_channel: false);
                    break;
            }
        }

        private static async Task SendResponse(ILogger logger, Slack.Slash slashData, string text, string userToken = null, bool in_channel = true)
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

        private static async Task HandleAsbWhitelistCommand(Slack.Slash slashData, IAsyncCollector<Resource> documentCollector, DocumentClient docClient, ILogger logger)
        {
            logger.LogInformation(nameof(HandleAsbWhitelistCommand));
            if (slashData.text.Split(' ').Length != 2)
            {
                await SendResponse(logger, slashData, "That is not a valid usage of that command.", in_channel: false);
                return;
            }
            else if (!WhitelistableCommands.Contains(slashData.text.Split(' ')[1]))
            {
                await SendResponse(logger, slashData, $"`{slashData.text.Split(' ')[1]}` is not a valid slash command to whitelist.", in_channel: false);
                return;
            }

            var whitelistBits = slashData.text.Split(' ')[1];
            Documents.Whitelist doc = null;
            try
            {
                logger.LogInformation("Attempting to get existing record...");
                doc = await docClient.ReadDocumentAsync<Documents.Whitelist>(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, whitelistBits.Substring(1)), new RequestOptions { PartitionKey = new PartitionKey(nameof(Documents.Whitelist) + "|command") });
                logger.LogInformation("Existing record found.");
            }
            catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("Existing record not found.");
                doc = new Documents.Whitelist { Subtype = "command", Id = whitelistBits.Substring(1), values = new HashSet<string>() };
            }

            if (slashData.text.StartsWith("add"))
            {
                doc.values.Add(slashData.channel_id);
                await documentCollector.AddAsync(doc);
                await SendResponse(logger, slashData, $"Added to `{whitelistBits}` whitelist for this channel :thumbsup:");
                SR.Deit();
            }
            else if (slashData.text.StartsWith("remove"))
            {
                if (!doc.values.Contains(slashData.channel_id))
                    await SendResponse(logger, slashData, $"`{whitelistBits}` wasn't on the whitelist for this channel :facepalm:", in_channel: false);
                else
                {
                    doc.values.Remove(slashData.channel_id);
                    await documentCollector.AddAsync(doc);
                    await SendResponse(logger, slashData, $"Removed `{whitelistBits}` from whitelist for this channel :thumbsup:");
                    SR.Deit();
                }
            }
            else
            {
                await SendResponse(logger, slashData, $"I don't know how to interpret `{slashData.text}`", in_channel: false);
            }
        }
    }
}
