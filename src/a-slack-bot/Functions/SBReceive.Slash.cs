using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SBReceive
    {
        private static readonly HashSet<string> WhitelistableCommands = new HashSet<string>
        {
            "/blackjack"
        };

        [FunctionName(nameof(SBReceiveSlash))]
        public static async Task SBReceiveSlash(
            [ServiceBusTrigger(C.SBQ.InputSlash)]Messages.ServiceBusInputSlash slashMessage,
            [DocumentDB(C.CDB.DN, C.CDB.CN, ConnectionStringSetting = C.CDB.CSS, PartitionKey = C.CDB.P, CreateIfNotExists = true)]IAsyncCollector<Resource> documentCollector,
            [DocumentDB(ConnectionStringSetting = C.CDB.CSS)]DocumentClient docClient,
            [ServiceBus(C.SBQ.Blackjack)]IAsyncCollector<BrokeredMessage> blackjackMessageCollector,
            [ServiceBus(C.SBQ.SendMessage)]IAsyncCollector<BrokeredMessage> messageCollector,
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
                case "/asb-response":
                    if (slashData.text == "help" || string.IsNullOrWhiteSpace(slashData.text))
                        await SendEphemeralResponse(
                            logger,
                            slashData,
                            "Add, list, or remove custom message responses. Syntax:" + @"```
add `key` some random text      Add a single response.
addb `key` text1||text2         Add responses for a key in bulk, '||'-separated.
list                            List all keys.
list `key`                      List all responses for a key.
remove `key` id                 Remove a single response.
```");
                    else
                        await HandleAsbResponseCommand(slashData, docClient, logger);
                    break;

                case "/asb-send-as-me":
                    if (slashData.text == "help" || string.IsNullOrWhiteSpace(slashData.text))
                        await SendEphemeralResponse(logger, slashData, "Visit https://api.slack.com/custom-integrations/legacy-tokens to generate a token, or send `clear` to remove your existing token.");
                    else if (slashData.text == "clear")
                    {
                        await docClient.DeleteDocumentAsync(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, slashData.user_id), new RequestOptions { PartitionKey = new PartitionKey(nameof(Documents.OAuthToken) + "|user") });
                        await SendEphemeralResponse(logger, slashData, "token cleared :thumbsup:");
                        SR.Deit();
                    }
                    else
                    {
                        await documentCollector.AddAsync(new Documents.OAuthToken { Subtype = "user", user_id = slashData.user_id, token = slashData.text });
                        await SendEphemeralResponse(logger, slashData, "token added :thumbsup:");
                        SR.Deit();
                    }
                    break;

                case "/asb-whitelist":
                    await HandleAsbWhitelistCommand(slashData, documentCollector, docClient, messageCollector, logger);
                    break;

                case "/blackjack":
                    if (!SR.W.CommandsChannels.ContainsKey("blackjack") || !SR.W.CommandsChannels["blackjack"].Contains(slashData.channel_id))
                        await SendEphemeralResponse(logger, slashData, $"`{slashData.command}` is not whitelisted for this channel. See `/asb-whitelist` to add it.");
                    else if (slashData.text == "help")
                        await SendEphemeralResponse(logger, slashData, @"Start a game of blackjack with some overrides available.
Syntax:
```
/blackjack          Start a game of blackjack.
/blackjack balance  Get your balance.
/blackjack balances Show all balances for all players.
```");
                    else if (slashData.text == "balance")
                        await blackjackMessageCollector.AddAsync(new BrokeredMessage(new Messages.ServiceBusBlackjack { channel_id = slashData.channel_id, user_id = slashData.user_id, type = Messages.BlackjackMessageType.GetBalance }));
                    else if (slashData.text == "balances")
                        await blackjackMessageCollector.AddAsync(new BrokeredMessage(new Messages.ServiceBusBlackjack { channel_id = slashData.channel_id, type = Messages.BlackjackMessageType.GetBalances }));
                    else if (!string.IsNullOrWhiteSpace(slashData.text))
                        await messageCollector.AddAsync(slashData, $"I don't know about '{slashData.text}'");
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0.5));
                        var threadStart = await SBSend.SendMessage(new Slack.Events.Inner.message { channel = slashData.channel_id, text = $"<@{slashData.user_id}> wants to start a game of blackjack! Open this thread to play." }, logger);
                        var game = new Documents.Blackjack { user_start = slashData.user_id, channel_id = slashData.channel_id, thread_ts = threadStart.message.ts, users = new List<string> { slashData.user_id }, hands = new Dictionary<string, List<Cards.Cards>> { [slashData.user_id] = new List<Cards.Cards>() } };
                        await documentCollector.AddAsync(game);
                        await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = slashData.channel_id, ts = threadStart.message.ts, text = $"<@{slashData.user_id}> wants to start a game of blackjack! Open this thread to play. Friendly game ref: {game.friendly_name}" });
                        await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = threadStart.channel, text = "Type `join` to join or `start` when ready to play. Starting in 1 minute.", thread_ts = threadStart.message.ts });
                        await blackjackMessageCollector.AddAsync(
                            new BrokeredMessage(
                                new Messages.ServiceBusBlackjack
                                {
                                    channel_id = slashData.channel_id,
                                    thread_ts = threadStart.message.ts,
                                    type = Messages.BlackjackMessageType.Timer_Joining
                                })
                            {
                                ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddMinutes(1)
                            });
                    }
                    break;

                case "/disapprove":
                    await SendUserResponse(logger, slashData, "ಠ_ಠ", userToken);
                    break;

                case "/flip":
                    await SendUserResponse(logger, slashData, slashData.text + " (╯°□°)╯︵ ┻━┻", userToken);
                    break;

                case "/spaces":
                    var text = slashData.text;
                    StringBuilder sb = new StringBuilder(text.Length * 2);
                    var enumerator = StringInfo.GetTextElementEnumerator(text);
                    while (enumerator.MoveNext())
                        sb.Append(enumerator.GetTextElement()).Append(' ');
                    await SendUserResponse(logger, slashData, sb.ToString(), userToken);
                    break;

                default:
                    await SendEphemeralResponse(logger, slashData, "*NOT SUPPORTED*");
                    break;
            }
        }

        private static async Task SendUserResponse(ILogger logger, Slack.Slash slashData, string text, string userToken)
        {
            logger.LogInformation("{0}: {1} {2} {3}", slashData.response_url, text, slashData.user_id, slashData.command);

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

        private static async Task SendEphemeralResponse(ILogger logger, Slack.Slash slashData, string text)
        {
            logger.LogInformation("{0}: {1} {2} {3}", slashData.response_url, text, slashData.user_id, slashData.command);

            var response = await httpClient.PostAsJsonAsync(
                slashData.response_url,
                new
                {
                    response_type = "ephemeral",
                    text
                });
            logger.LogInformation("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        private static async Task HandleAsbResponseCommand(Slack.Slash slashData, DocumentClient docClient, ILogger logger)
        {
            logger.LogInformation(nameof(HandleAsbResponseCommand));
            if (slashData.text == "list")
            {
                logger.LogInformation("Retrieving list of all custom response keys...");
                var query = docClient.CreateDocumentQuery<string>(
                    UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                    $"SELECT DISTINCT VALUE r.{nameof(Documents.Response.Subtype)} FROM r WHERE r.{nameof(Documents.BaseDocument.Type)} = '{nameof(Documents.Response)}'",
                    new FeedOptions { EnableCrossPartitionQuery = true })
                    .AsDocumentQuery();
                var results = await query.GetAllResults(logger);

                await SendEphemeralResponse(logger, slashData, $"Keys in use: `{string.Join("`|`", results)}`");
                return;
            }

            // Now proceed to more "normal" commands that at least look the same
            if (!(slashData.text.StartsWith("add `") || slashData.text.StartsWith("list `") || slashData.text.StartsWith("remove `")))
            {
                await SendEphemeralResponse(logger, slashData, "Did not detect a valid start for that command.");
                return;
            }
            else if (slashData.text.Count(c => c == '`') < 2)
            {
                await SendEphemeralResponse(logger, slashData, "Did not detect enough backtick characters for that command.");
                return;
            }

            var instruction = slashData.text.Split(' ')[0];
            var key = slashData.text.Split('`')[1].Trim().ToLowerInvariant();
            var value = slashData.text.Substring(slashData.text.IndexOf('`', slashData.text.IndexOf('`') + 1) + 1).Trim();

            switch (instruction)
            {
                case "add":
                    logger.LogInformation("Attempting to add new custom response...");
                    var doc = await docClient.CreateDocumentAsync(
                        UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                        new Documents.Response
                        {
                            Id = Guid.NewGuid().ToString().Split('-')[0],
                            Subtype = key,
                            Content = value,
                            user_id = slashData.user_id
                        },
                        new RequestOptions { PartitionKey = new PartitionKey(nameof(Documents.Response) + "|" + key) },
                        disableAutomaticIdGeneration: true);
                    await SendEphemeralResponse(logger, slashData, $"Added: `{key}` (`{doc.Resource.Id}`) {value}");
                    SR.Deit();
                    break;

                case "list":
                    logger.LogInformation("Retrieving list of all custom responses for specified key...");
                    var query = docClient.CreateDocumentQuery<Documents.Response>(
                        UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                        new FeedOptions { PartitionKey = new PartitionKey(nameof(Documents.Response) + "|" + key) })
                        .AsDocumentQuery();
                    var results = await query.GetAllResults(logger);
                    await SendEphemeralResponse(logger, slashData, $"Key: `{key}` Values:\n{string.Join("\n\n", results.Select(r => $"`{r.Id}` {r.Content}"))}");
                    break;

                case "remove":
                    try
                    {
                        logger.LogInformation("Attempting to remove existing record...");
                        await docClient.DeleteDocumentAsync(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, value), new RequestOptions { PartitionKey = new PartitionKey(nameof(Documents.Response) + "|" + key) });
                        await SendEphemeralResponse(logger, slashData, $"Removed `{key}`: {value}");
                        SR.Deit();
                    }
                    catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
                    {
                        await SendEphemeralResponse(logger, slashData, $"Error! `{key}` ({value}) not found!");
                    }
                    break;
            }
        }

        private static async Task HandleAsbWhitelistCommand(Slack.Slash slashData, IAsyncCollector<Resource> documentCollector, DocumentClient docClient, IAsyncCollector<BrokeredMessage> messageCollector, ILogger logger)
        {
            logger.LogInformation(nameof(HandleAsbWhitelistCommand));
            if (slashData.text.Split(' ').Length != 2)
            {
                await SendEphemeralResponse(logger, slashData, "That is not a valid usage of that command.");
                return;
            }
            else if (!WhitelistableCommands.Contains(slashData.text.Split(' ')[1]))
            {
                await SendEphemeralResponse(logger, slashData, $"`{slashData.text.Split(' ')[1]}` is not a valid slash command to whitelist.");
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
                await messageCollector.AddAsync(slashData, $"Added to `{whitelistBits}` whitelist for this channel :thumbsup:");
                SR.Deit();
            }
            else if (slashData.text.StartsWith("remove"))
            {
                if (!doc.values.Contains(slashData.channel_id))
                    await SendEphemeralResponse(logger, slashData, $"`{whitelistBits}` wasn't on the whitelist for this channel :facepalm:");
                else
                {
                    doc.values.Remove(slashData.channel_id);
                    await documentCollector.AddAsync(doc);
                    await messageCollector.AddAsync(slashData, $"Removed `{whitelistBits}` from whitelist for this channel :thumbsup:");
                    SR.Deit();
                }
            }
            else
            {
                await SendEphemeralResponse(logger, slashData, $"I don't know how to interpret `{slashData.text}`");
            }
        }
    }
}
