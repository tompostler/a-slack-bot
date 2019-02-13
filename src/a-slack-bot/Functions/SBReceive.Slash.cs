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
            "/blackjack",
            "/guess"
        };

        private static readonly HashSet<string> StandingsCommands = new HashSet<string>
        {
            "/balance",
            "/balances",
            "/guess"
        };

        [FunctionName(nameof(SBReceiveSlash))]
        public static async Task SBReceiveSlash(
            [ServiceBusTrigger(C.SBQ.InputSlash)]Messages.ServiceBusInputSlash slashMessage,
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

            // Grab the standings doc upfront when necessary
            Documents.Standings standings = null;
            if (StandingsCommands.Contains(slashData.command))
                try
                {
                    standings = await docClient.ReadDocumentAsync<Documents.Standings>(Documents.Standings.DocUri, new RequestOptions { PartitionKey = new Documents.Standings().PK });
                }
                catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
                {
                    await docClient.CreateDocumentAsync(
                        C.CDB.DCUri,
                        new Documents.Standings(),
                        new RequestOptions { PartitionKey = new Documents.Standings().PK });

                    // Let SB retry us. Should only ever hit this once.
                    throw;
                }

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
                        await docClient.DeleteDocumentAsync(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, slashData.user_id), new RequestOptions { PartitionKey = new PartitionKey(null) });
                        await ephemeralMessageCollector.AddEAsync(slashData, "token cleared :thumbsup:");
                        SR.Deit();
                    }
                    else
                    {
                        var tokDoc = new Documents.OAuthUserToken { Id = slashData.user_id, token = slashData.text };
                        await docClient.UpsertDocumentAsync(
                            C.CDB.DCUri,
                            tokDoc,
                            new RequestOptions { PartitionKey = tokDoc.PK },
                            disableAutomaticIdGeneration: true);
                        await ephemeralMessageCollector.AddEAsync(slashData, "token added :thumbsup:");
                        SR.Deit();
                    }
                    break;


                case "/asb-whitelist":
                    await HandleAsbWhitelistCommand(slashData, docClient, messageCollector, ephemeralMessageCollector, logger);
                    break;


                case "/balance":
                    long balance = 10_000;
                    if (standings.bals.ContainsKey(slashData.user_id))
                        balance = standings.bals[slashData.user_id];
                    await messageCollector.AddAsync(slashData, $"<@{slashData.user_id}> has ¤{balance:#,#}");
                    break;


                case "/balances":
                    var bals = new StringBuilder();
                    bals.AppendLine("Balances for those that have played:");
                    bals.AppendLine("```");
                    var maxNamLength = Math.Max(SR.U.MaxNameLength, 8);
                    var maxBalLength = Math.Max($"{(standings.bals.Values.Count == 0 ? 0 : standings.bals.Values.Max()):#,#}".Length, 7);
                    bals.Append("USER".PadRight(SR.U.MaxNameLength));
                    bals.Append("  ");
                    bals.Append("BALANCE".PadLeft(maxBalLength));
                    bals.AppendLine();
                    foreach (var user in standings.bals)
                    {
                        if (SR.U.IdToName.ContainsKey(user.Key))
                            bals.Append($"{SR.U.IdToName[user.Key].PadRight(maxNamLength)}  ");
                        else
                            bals.Append($"{user.Key.PadRight(maxNamLength)}  ");
                        bals.AppendFormat($"{{0,{maxBalLength}:#,#}}", user.Value);
                        bals.AppendLine();
                    }
                    bals.AppendLine("```");
                    bals.AppendLine();
                    bals.AppendFormat("Balances for those that have not played: ¤{0:#,#}", 10_000);
                    await messageCollector.AddAsync(slashData, bals.ToString());
                    break;


                case "/blackjack":
                    if (!SR.W.CommandsChannels.ContainsKey(slashData.command) || !SR.W.CommandsChannels[slashData.command].Contains(slashData.channel_id))
                        await ephemeralMessageCollector.AddEAsync(slashData, $"`{slashData.command}` is not whitelisted for this channel. See `/asb-whitelist` to add it.");
                    else if (slashData.text == "help")
                        await messageCollector.AddAsync(slashData, "Start a game of blackjack by saying `/blackjack`.\nTo view your balances, use `/balance`.");
                    else if (!string.IsNullOrWhiteSpace(slashData.text))
                        await messageCollector.AddAsync(slashData, $"I don't know about '{slashData.text}'");
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0.5));
                        var threadStart = await SBSend.SendMessage(new Slack.Events.Inner.message { channel = slashData.channel_id, text = $"<@{slashData.user_id}> wants to start a game of blackjack! Open this thread to play." }, logger);
                        var game = new Documents.Blackjack { user_start = slashData.user_id, channel_id = slashData.channel_id, thread_ts = threadStart.message.ts, users = new List<string> { slashData.user_id }, hands = new Dictionary<string, List<Cards.Cards>> { [slashData.user_id] = new List<Cards.Cards>() } };
                        await docClient.UpsertDocumentAsync(
                            C.CDB.DCUri,
                            game,
                            new RequestOptions { PartitionKey = game.PK });
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


                case "/guess":
                    if (!SR.W.CommandsChannels.ContainsKey(slashData.command) || !SR.W.CommandsChannels[slashData.command].Contains(slashData.channel_id))
                        await ephemeralMessageCollector.AddEAsync(slashData, $"`{slashData.command}` is not whitelisted for this channel. See `/asb-whitelist` to add it.");
                    else if (slashData.text == "help")
                        await messageCollector.AddAsync(slashData, "Guess a number in the interval `(0, balance]` (see `/balance`). Rewards guesses close to the number I'm thinking of, but be more than 100 away and you start losing!");
                    else
                        await HandleGuessCommand(slashData, standings, docClient, messageCollector, ephemeralMessageCollector, logger);
                    break;


                case "/password":
                    if (slashData.text == "help")
                        await messageCollector.AddAsync(slashData, "Generates a secure random password. You may optionally specify a length in (0,256); default is 32.");
                    if (!int.TryParse(slashData.text, out int length) || length <= 0 || length > 256)
                        length = 32;
                    const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789~!@#$%^*()_+-=[]{}\\|;:'\",./? ";
                    var psb = new StringBuilder(length);
                    var bytes = new byte[length];
                    SR.RNGCSP.GetBytes(bytes);
                    foreach (var @byte in bytes)
                        psb.Append(validChars[@byte % validChars.Length]);
                    await messageCollector.AddAsync(slashData, $"`{psb.ToString()}`");
                    break;


                case "/spaces":
                    var text = slashData.text;
                    var sb = new StringBuilder(text.Length * 2);
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
                    C.CDB.DCUri,
                    $"SELECT DISTINCT VALUE r.{nameof(Documents.Response.key)} FROM r WHERE STARTSWITH(r.{nameof(Documents.Base.doctype)}, '{nameof(Documents.Response)}|')",
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
                    var resp = new Documents.Response
                    {
                        Id = Guid.NewGuid().ToString().Split('-')[0],
                        key = key,
                        value = value,
                        user_id = slashData.user_id
                    };
                    var doc = await docClient.CreateDocumentAsync(
                        C.CDB.DCUri,
                        resp,
                        new RequestOptions { PartitionKey = resp.PK },
                        disableAutomaticIdGeneration: true);
                    await ephemeralMessageCollector.AddEAsync(slashData, $"Added: `{key}` (`{doc.Resource.Id}`) {value}");
                    SR.Deit();
                    break;

                case "addb":
                    logger.LogInformation("Attempting to bulk add new custom responses...");
                    foreach (var valueb in value.Split(new string[] { "||" }, StringSplitOptions.RemoveEmptyEntries).Select(_ => _.Trim()))
                    {
                        var valuec = await ReplaceImageURIs(key, valueb, logger);
                        resp = new Documents.Response
                        {
                            Id = Guid.NewGuid().ToString().Split('-')[0],
                            key = key,
                            value = valuec,
                            user_id = slashData.user_id
                        };
                        doc = await docClient.CreateDocumentAsync(
                            C.CDB.DCUri,
                            resp,
                            new RequestOptions { PartitionKey = resp.PK },
                            disableAutomaticIdGeneration: true);
                        await ephemeralMessageCollector.AddEAsync(slashData, $"Added: `{key}` (`{doc.Resource.Id}`) {valuec}");
                    }
                    SR.Deit();
                    break;

                case "list":
                    logger.LogInformation("Retrieving list of all custom responses for specified key...");
                    var query = docClient.CreateDocumentQuery<Documents.Response>(
                        C.CDB.DCUri,
                        "SELECT * FROM r",
                        new FeedOptions { PartitionKey = new PartitionKey($"{nameof(Documents.Response)}|{key}") })
                        .AsDocumentQuery();
                    var results = (await query.GetAllResults(logger)).ToList();
                    results.Sort((r1, r2) => r1.Id.CompareTo(r2.Id));
                    await ephemeralMessageCollector.AddEAsync(slashData, $"Key: `{key}` Values:\n{string.Join("\n\n", results.Select(r => $"`{r.Id}` {r.value}"))}");
                    break;

                case "remove":
                    try
                    {
                        logger.LogInformation("Attempting to remove existing record...");
                        await docClient.DeleteDocumentAsync(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, value), new RequestOptions { PartitionKey = new PartitionKey($"{nameof(Documents.Response)}|{key}") });
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
                catch (OutOfMemoryException oome)
                {
                    logger.LogInformation("Was invalid: {0}", oome);
                }
                catch (ArgumentException ae)
                {
                    logger.LogInformation("Was invalid: {0}", ae);
                }
                GC.Collect();
            }
            return text;
        }

        private static async Task HandleAsbWhitelistCommand(
            Slack.Slash slashData,
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
                doc = await docClient.ReadDocumentAsync<Documents.Whitelist>(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, "command"), new RequestOptions { PartitionKey = new Documents.Whitelist().PK });
                logger.LogInformation("Existing record found.");
            }
            catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("Existing record not found.");
                doc = new Documents.Whitelist { Id = "command", values = new Dictionary<string, HashSet<string>>() };
            }

            if (slashData.text.StartsWith("add"))
            {
                if (!doc.values.ContainsKey(whitelistBits))
                    doc.values.Add(whitelistBits, new HashSet<string>());
                doc.values[whitelistBits].Add(slashData.channel_id);
                await docClient.UpsertDocumentAsync(
                    C.CDB.DCUri,
                    doc,
                    new RequestOptions { PartitionKey = doc.PK },
                    disableAutomaticIdGeneration: true);
                await messageCollector.AddAsync(slashData, $"Added to `{whitelistBits}` whitelist for this channel :thumbsup:");
                SR.Deit();
            }
            else if (slashData.text.StartsWith("remove"))
            {
                if (doc == null || !doc.values.ContainsKey(whitelistBits) || !doc.values[whitelistBits].Contains(slashData.channel_id))
                    await ephemeralMessageCollector.AddEAsync(slashData, $"`{whitelistBits}` wasn't on the whitelist for this channel :facepalm:");
                else
                {
                    doc.values[whitelistBits].Remove(slashData.channel_id);
                    await docClient.UpsertDocumentAsync(
                        C.CDB.DCUri,
                        doc,
                        new RequestOptions { PartitionKey = doc.PK },
                        disableAutomaticIdGeneration: true);
                    await messageCollector.AddAsync(slashData, $"Removed `{whitelistBits}` from whitelist for this channel :thumbsup:");
                    SR.Deit();
                }
            }
            else
            {
                await ephemeralMessageCollector.AddEAsync(slashData, $"I don't know how to interpret `{slashData.text}`");
            }
        }

        private static async Task HandleGuessCommand(
            Slack.Slash slashData,
            Documents.Standings standings,
            DocumentClient docClient,
            IAsyncCollector<BrokeredMessage> messageCollector,
            IAsyncCollector<BrokeredMessage> ephemeralMessageCollector,
            ILogger logger)
        {
            if (!long.TryParse(slashData.text, out long guess) || guess <= 0)
            {
                await messageCollector.AddAsync(slashData, $"`{slashData.text}` could not be parsed as a positive number.");
                return;
            }

            long balance = 10_000;
            if (standings.bals.ContainsKey(slashData.user_id))
                balance = standings.bals[slashData.user_id];
            else
                standings.bals.Add(slashData.user_id, balance);

            if (guess > balance)
            {
                // Lose at most 2.5% of total balance
                var losspct = SR.Rand.NextDouble() * 0.025;
                logger.LogInformation("Loss percent {0} for {1}", losspct, slashData.user_id);
                var loss = (long)Math.Max(losspct * balance, 1);
                standings.bals[slashData.user_id] -= loss;
                await messageCollector.AddAsync(slashData, $"<@{slashData.user_id}> bet ¤{guess:#,#} which is more than their current balance of ¤{balance:#,#}. They lose ¤{loss:#,#} ({losspct:p}) as a penalty for trying to game the system.");
            }
            else
            {
                // Take 100 divided by the distance from guess plus one
                long thinking = (long)(SR.Rand.NextDouble() * balance) + 1;
                long closeness = Math.Abs(guess - thinking);
                double amount = 100d / (closeness + 1);
                long winnings = (long)Math.Ceiling(amount);
                if (amount < 1)
                    winnings = -(long)Math.Ceiling(1d / amount);
                standings.bals[slashData.user_id] += winnings;
                await messageCollector.AddAsync(slashData, $"<@{slashData.user_id}> guessed {guess}, and I was thinking of {thinking:#,#}. They {(winnings >= 0 ? "win" : "lose")} ¤{winnings:#,#} for {(closeness == 0 ? "guessing correctly" : $"being {closeness:#,#} off")} and are now at ¤{standings.bals[slashData.user_id]:#,#}.");
            }

            // Make sure they still have some money...
            if (standings.bals[slashData.user_id] <= 0)
            {
                standings.bals[slashData.user_id] = 1;
                await messageCollector.AddAsync(slashData, $"However, <@{slashData.user_id}> is so poor their balance was forced to ¤1.");
            }

            await docClient.UpsertDocumentAsync(
                C.CDB.DCUri,
                standings,
                new RequestOptions { PartitionKey = standings.PK },
                disableAutomaticIdGeneration: true);
        }
    }
}
