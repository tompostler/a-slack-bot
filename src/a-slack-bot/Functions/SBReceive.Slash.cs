using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
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
            [ServiceBus(C.SBQ.SendMessageEphemeral)]IAsyncCollector<BrokeredMessage> ephemeralMessageCollector,
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
                        await ephemeralMessageCollector.AddEAsync(
                            slashData,
                            "Add, list, or remove custom message responses. Syntax:" + @"```
add `key` some random text      Add a single response.
addb `key` text1||text2         Add responses for a key in bulk, '||'-separated.
list                            List all keys.
list `key`                      List all responses for a key.
remove `key` id                 Remove a single response.
```");
                    else
                        await HandleAsbResponseCommand(slashData, docClient, ephemeralMessageCollector, logger);
                    break;

                case "/asb-send-as-me":
                    if (slashData.text == "help" || string.IsNullOrWhiteSpace(slashData.text))
                        await ephemeralMessageCollector.AddEAsync(slashData, "Visit https://api.slack.com/custom-integrations/legacy-tokens to generate a token, or send `clear` to remove your existing token.");
                    else if (slashData.text == "clear")
                    {
                        await docClient.DeleteDocumentAsync(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, slashData.user_id), new RequestOptions { PartitionKey = new PartitionKey(nameof(Documents.OAuthToken) + "|user") });
                        await ephemeralMessageCollector.AddEAsync(slashData, "token cleared :thumbsup:");
                        SR.Deit();
                    }
                    else
                    {
                        await documentCollector.AddAsync(new Documents.OAuthToken { Subtype = "user", user_id = slashData.user_id, token = slashData.text });
                        await ephemeralMessageCollector.AddEAsync(slashData, "token added :thumbsup:");
                        SR.Deit();
                    }
                    break;

                case "/asb-whitelist":
                    await HandleAsbWhitelistCommand(slashData, documentCollector, docClient, messageCollector, ephemeralMessageCollector, logger);
                    break;

                case "/blackjack":
                    if (!SR.W.CommandsChannels.ContainsKey("blackjack") || !SR.W.CommandsChannels["blackjack"].Contains(slashData.channel_id))
                        await ephemeralMessageCollector.AddEAsync(slashData, $"`{slashData.command}` is not whitelisted for this channel. See `/asb-whitelist` to add it.");
                    else if (slashData.text == "help")
                        await ephemeralMessageCollector.AddEAsync(slashData, @"Start a game of blackjack with some overrides available.
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
                        await messageCollector.AddAsync(new Slack.Events.Inner.message { channel = slashData.channel_id, ts = threadStart.message.ts, text = $"Game id: {game.friendly_name}\n<@{slashData.user_id}> wants to start a game of blackjack! Open this thread to play." });
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
                    await SendUserResponse(slashData, "ಠ_ಠ", userToken, ephemeralMessageCollector, logger);
                    break;

                case "/flip":
                    await SendUserResponse(slashData, slashData.text + " (╯°□°)╯︵ ┻━┻", userToken, ephemeralMessageCollector, logger);
                    break;

                case "/spaces":
                    var text = slashData.text;
                    StringBuilder sb = new StringBuilder(text.Length * 2);
                    var enumerator = StringInfo.GetTextElementEnumerator(text);
                    while (enumerator.MoveNext())
                        sb.Append(enumerator.GetTextElement()).Append(' ');
                    await SendUserResponse(slashData, sb.ToString(), userToken, ephemeralMessageCollector, logger);
                    break;

                default:
                    await ephemeralMessageCollector.AddEAsync(slashData, "*NOT SUPPORTED*");
                    break;
            }
        }

        private static async Task SendUserResponse(Slack.Slash slashData, string text, string userToken, IAsyncCollector<BrokeredMessage> ephemeralMessageCollector, ILogger logger)
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
                    await ephemeralMessageCollector.AddEAsync(slashData, "Your user token is invalid.");
                    logger.LogWarning("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
                }
                else
                {
                    await ephemeralMessageCollector.AddEAsync(slashData, $"Something went wrong: `{responseObj.error}`");
                    logger.LogError("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
                }
            }
        }

        private static async Task HandleAsbResponseCommand(
            Slack.Slash slashData,
            DocumentClient docClient,
            IAsyncCollector<BrokeredMessage> ephemeralMessageCollector,
            ILogger logger)
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
                var results = (await query.GetAllResults(logger)).ToList();
                results.Sort();

                await ephemeralMessageCollector.AddEAsync(slashData, $"Keys in use: `{string.Join("` | `", results)}`");
                return;
            }

            // Now proceed to more "normal" commands that at least look the same
            if (!(slashData.text.StartsWith("add `") || slashData.text.StartsWith("addb `") || slashData.text.StartsWith("list `") || slashData.text.StartsWith("remove `")))
            {
                await ephemeralMessageCollector.AddEAsync(slashData, "Did not detect a valid start for that command.");
                return;
            }
            else if (slashData.text.Count(c => c == '`') < 2)
            {
                await ephemeralMessageCollector.AddEAsync(slashData, "Did not detect enough backtick characters for that command.");
                return;
            }

            var instruction = slashData.text.Split(' ')[0];
            var key = slashData.text.Split('`')[1].Trim().ToLowerInvariant();
            var value = SR.MessageSlackEncode(slashData.text.Substring(slashData.text.IndexOf('`', slashData.text.IndexOf('`') + 1) + 1).Trim());

            switch (instruction)
            {
                case "add":
                    logger.LogInformation("Attempting to add new custom response...");
                    value = await ReplaceImageURIs(key, value, logger);
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
                    await ephemeralMessageCollector.AddEAsync(slashData, $"Added: `{key}` (`{doc.Resource.Id}`) {value}");
                    SR.Deit();
                    break;

                case "addb":
                    logger.LogInformation("Attempting to bulk add new custom responses...");
                    foreach (var valueb in value.Split(new string[] { "||" }, StringSplitOptions.RemoveEmptyEntries).Select(_ => _.Trim()))
                    {
                        var valuec = await ReplaceImageURIs(key, valueb, logger);
                        doc = await docClient.CreateDocumentAsync(
                            UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                            new Documents.Response
                            {
                                Id = Guid.NewGuid().ToString().Split('-')[0],
                                Subtype = key,
                                Content = valuec,
                                user_id = slashData.user_id
                            },
                            new RequestOptions { PartitionKey = new PartitionKey(nameof(Documents.Response) + "|" + key) },
                            disableAutomaticIdGeneration: true);
                        await ephemeralMessageCollector.AddEAsync(slashData, $"Added: `{key}` (`{doc.Resource.Id}`) {valuec}");
                    }
                    SR.Deit();
                    break;

                case "list":
                    logger.LogInformation("Retrieving list of all custom responses for specified key...");
                    var query = docClient.CreateDocumentQuery<Documents.Response>(
                        UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                        $"SELECT * FROM r WHERE r.id <> '{nameof(Documents.ResponsesUsed)}'",
                        new FeedOptions { PartitionKey = new PartitionKey(nameof(Documents.Response) + "|" + key) })
                        .AsDocumentQuery();
                    var results = (await query.GetAllResults(logger)).ToList();
                    results.Sort((r1,r2) => r1.Id.CompareTo(r2.Id));
                    await ephemeralMessageCollector.AddEAsync(slashData, $"Key: `{key}` Values:\n{string.Join("\n\n", results.Select(r => $"`{r.Id}` {r.Content}"))}");
                    break;

                case "remove":
                    try
                    {
                        logger.LogInformation("Attempting to remove existing record...");
                        await docClient.DeleteDocumentAsync(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, value), new RequestOptions { PartitionKey = new PartitionKey(nameof(Documents.Response) + "|" + key) });
                        await ephemeralMessageCollector.AddEAsync(slashData, $"Removed `{key}`: {value}");
                        SR.Deit();
                    }
                    catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
                    {
                        await ephemeralMessageCollector.AddEAsync(slashData, $"Error! `{key}` ({value}) not found!");
                    }
                    break;
            }
        }

        private static Regex ShortURI = new Regex(@"((https?|ftp):)?\/\/[^\s\/$.?#].[^\s]*", RegexOptions.Compiled);
        private static async Task<string> ReplaceImageURIs(string key, string text, ILogger logger)
        {
            var client = CloudStorageAccount.Parse(Settings.AzureWebJobsStorage).CreateCloudBlobClient();
            var blobContainer = client.GetContainerReference(Settings.BlobContainerName);
            var matches = ShortURI.Matches(text);
            for (int i = 0; i < matches.Count; i++)
            {
                var matchUri = matches[i].Value;
                logger.LogInformation("Attempting to download: {0}", matchUri);
                var response = await httpClient.GetAsync(matchUri);
                try
                {
                    var stream = await response.Content.ReadAsStreamAsync();
                    Image.FromStream(stream);
                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    logger.LogInformation("Was valid. Replacing...");

                    var imageName = matchUri.Substring(matchUri.LastIndexOf('/'));
                    var blob = blobContainer.GetBlockBlobReference(key.Replace(' ', '-') + imageName);
                    await blob.UploadFromStreamAsync(stream);
                    text = text.Replace(matchUri, blob.Uri.AbsoluteUri);
                    logger.LogInformation("Replaced with {0}", blob.Uri.AbsoluteUri);
                }
                catch (OutOfMemoryException)
                { }
                GC.Collect();
            }
            return text;
        }

        private static async Task HandleAsbWhitelistCommand(
            Slack.Slash slashData,
            IAsyncCollector<Resource> documentCollector,
            DocumentClient docClient,
            IAsyncCollector<BrokeredMessage> messageCollector,
            IAsyncCollector<BrokeredMessage> ephemeralMessageCollector,
            ILogger logger)
        {
            logger.LogInformation(nameof(HandleAsbWhitelistCommand));
            if (slashData.text.Split(' ').Length != 2)
            {
                await ephemeralMessageCollector.AddEAsync(slashData, "That is not a valid usage of that command.");
                return;
            }
            else if (!WhitelistableCommands.Contains(slashData.text.Split(' ')[1]))
            {
                await ephemeralMessageCollector.AddEAsync(slashData, $"`{slashData.text.Split(' ')[1]}` is not a valid slash command to whitelist.");
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
                    await ephemeralMessageCollector.AddEAsync(slashData, $"`{whitelistBits}` wasn't on the whitelist for this channel :facepalm:");
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
                await ephemeralMessageCollector.AddEAsync(slashData, $"I don't know how to interpret `{slashData.text}`");
            }
        }
    }
}
