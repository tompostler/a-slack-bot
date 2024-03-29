﻿using Microsoft.Azure.Documents;
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
using System.Security.Cryptography;
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
            [ServiceBusTrigger(C.SBQ.InputSlash)] Messages.InputSlash slashMessage,
            [DocumentDB(ConnectionStringSetting = C.CDB.CSS)] DocumentClient docClient,
            [ServiceBus(C.SBQ.Blackjack)] IAsyncCollector<BrokeredMessage> blackjackMessageCollector,
            [ServiceBus(C.SBQ.SendMessage)] IAsyncCollector<BrokeredMessage> messageCollector,
            [ServiceBus(C.SBQ.SendMessageEphemeral)] IAsyncCollector<BrokeredMessage> ephemeralMessageCollector,
            ILogger logger)
        {
            await SR.Init(logger);

            var slashData = slashMessage.slashData;
            logger.LogInformation("SlashData: {0}", JsonConvert.SerializeObject(slashData));

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
                case "/asb-reaction":
                    if (slashData.text == "help" || string.IsNullOrWhiteSpace(slashData.text))
                        await ephemeralMessageCollector.AddEAsync(
                            slashData,
                            "Add, list, or remove custom message reactions. Syntax:" + @"```
add `key` :emoji:           Add a single reaction.
addb `key` :emoji:||:emoji: Add reaction for a key in bulk, '||'-separated.
list                        List all keys.
list `key`                  List all reactions for a key.
remove `key` id             Remove a single reaction.
```");
                    else
                        await HandleAsbReThingCommand<Documents.Reaction>(slashData, docClient, ephemeralMessageCollector, logger);
                    break;


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
                        await HandleAsbReThingCommand<Documents.Response>(slashData, docClient, ephemeralMessageCollector, logger);
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
                    var sortedBals = standings.bals.Select(kvp => kvp).ToList();
                    sortedBals.Sort((t1, t2) => t2.Value.CompareTo(t1.Value));
                    foreach (var user in sortedBals)
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
                                new Messages.Blackjack
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
                    await SendUserResponse(slashData, "ಠ_ಠ", messageCollector, ephemeralMessageCollector, logger);
                    break;


                case "/fact":
                    var wikiResponse = await httpClient.GetAsync("https://en.wikipedia.org/w/api.php?action=query&format=json&prop=extracts%7Cinfo&generator=random&utf8=1&exintro=1&explaintext=1&inprop=url&grnnamespace=0");
                    var wikiResult = await wikiResponse.Content.ReadAsAsync<WikiResponse>();
                    await messageCollector.AddAsync(
                        new Slack.Events.Inner.message
                        {
                            channel = slashData.channel_id,
                            text = wikiResult.query.pages.FirstOrDefault().Value?.extract?.Split('\n')?.FirstOrDefault(),
                            attachments = new List<Slack.Events.Inner.message_parts.attachment>
                            {
                                new Slack.Events.Inner.message_parts.attachment
                                {
                                    text = string.Empty,
                                    footer = $"{slashData.command} from <@{slashData.user_id}>. <{wikiResult.query.pages.FirstOrDefault().Value?.fullurl}|{wikiResult.query.pages.FirstOrDefault().Value?.title}>, last modified {wikiResult.query.pages.FirstOrDefault().Value?.touched:yyyy-MM-dd}"
                                }
                            }
                        });
                    break;


                case "/flip":
                    await SendUserResponse(slashData, slashData.text + " (╯°□°)╯︵ ┻━┻", messageCollector, ephemeralMessageCollector, logger);
                    break;


                case "/guess":
                    if (!SR.W.CommandsChannels.ContainsKey(slashData.command) || !SR.W.CommandsChannels[slashData.command].Contains(slashData.channel_id))
                        await ephemeralMessageCollector.AddEAsync(slashData, $"`{slashData.command}` is not whitelisted for this channel. See `/asb-whitelist` to add it.");
                    else if (slashData.text == "help")
                        await messageCollector.AddAsync(slashData, "Guess a number in the interval `(0, balance]` (see `/balance`). Rewards guesses close to the number I'm thinking of, but be more than 100 away and you start losing!");
                    else
                        await HandleGuessCommand(slashData, standings, docClient, messageCollector, logger);
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
                    await SendUserResponse(slashData, sb.ToString(), messageCollector, ephemeralMessageCollector, logger);
                    break;


                default:
                    await ephemeralMessageCollector.AddEAsync(slashData, "*NOT SUPPORTED*");
                    break;
            }
        }

        private static async Task SendUserResponse(Slack.Slash slashData, string text, IAsyncCollector<BrokeredMessage> messageCollector, IAsyncCollector<BrokeredMessage> ephemeralMessageCollector, ILogger logger)
        {
            logger.LogInformation("{0}: {1} {2} {3}", slashData.response_url, text, slashData.user_id, slashData.command);

            // We need to decide if we should post as the user or as ourselves
            string userToken = default;
            if (SR.T.ChatWriteUser.ContainsKey(slashData.user_id))
                userToken = SR.T.ChatWriteUser[slashData.user_id];

            if (userToken == default)
            {
                // Post as ourselves, but also "blame" the user
                await messageCollector.AddAsync(
                    new Slack.Events.Inner.message
                    {
                        channel = slashData.channel_id,
                        text = text,
                        attachments = new List<Slack.Events.Inner.message_parts.attachment>
                        {
                            new Slack.Events.Inner.message_parts.attachment
                            {
                                text = string.Empty,
                                footer = $"<@{slashData.user_id}>, {slashData.command}"
                            }
                        }
                    });
            }
            else
            {
                // Try to post as the user
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

                // But if it didn't work then tell them why
                if (!responseObj.ok)
                {
                    if (responseObj.error == "invalid_auth" || responseObj.error == "token_revoked")
                    {
                        await ephemeralMessageCollector.AddEAsync(slashData, $"Your user token is {responseObj.error}.");
                        logger.LogWarning("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
                    }
                    else
                    {
                        await ephemeralMessageCollector.AddEAsync(slashData, $"Something went wrong: `{responseObj.error}`");
                        logger.LogError("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
                    }
                }
            }
        }

        private static async Task HandleAsbReThingCommand<T>(
            Slack.Slash slashData,
            DocumentClient docClient,
            IAsyncCollector<BrokeredMessage> ephemeralMessageCollector,
            ILogger logger)
            where T : Documents.ReThings, new()
        {
            logger.LogInformation(nameof(HandleAsbReThingCommand));
            if (slashData.text == "list")
            {
                logger.LogInformation("Retrieving list of all custom {0} keys...", typeof(T).Name);
                var query = docClient.CreateDocumentQuery<string>(
                    C.CDB.DCUri,
                    $"SELECT DISTINCT VALUE r.{nameof(Documents.ReThings.key)} FROM r WHERE STARTSWITH(r.{nameof(Documents.Base.doctype)}, '{typeof(T).Name}|')",
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
            var pk = new T { key = key }.PK;

            // Figure out what kind of trimming we need to do on the values
            Func<string, string> trimmer;
            if (typeof(T) == typeof(Documents.Reaction))
                trimmer = (string str) => str.Trim().Trim(':');
            else
                trimmer = (string str) => str.Trim();
            var value = SR.MessageSlackEncode(trimmer(slashData.text.Substring(slashData.text.IndexOf('`', slashData.text.IndexOf('`') + 1) + 1)));

            // And how to figure out if they're valid
            Func<string, bool> valid = (str) => true;
            if (typeof(T) == typeof(Documents.Reaction))
                valid = (string str) => SR.E.All.Contains(value);

            // And determine what the starting count should be
            var countQuery = docClient.CreateDocumentQuery<int>(
                C.CDB.DCUri,
                $"SELECT VALUE MIN(r.{nameof(Documents.ReThings.count)}) FROM r",
                new FeedOptions { PartitionKey = pk })
                .AsDocumentQuery();
            var minCount = (await countQuery.GetAllResults(logger)).SingleOrDefault();

            switch (instruction)
            {
                case "add":
                    logger.LogInformation("Attempting to add new custom {0}...", typeof(T).Name);
                    value = await ReplaceImageURIs(key, value, logger);
                    if (!valid(value))
                    {
                        await ephemeralMessageCollector.AddEAsync(slashData, $"Not valid: `{key}` {value}");
                        break;
                    }
                    var resp = new T
                    {
                        Id = Guid.NewGuid().ToString().Split('-')[0],
                        key = key,
                        value = value,
                        user_id = slashData.user_id,
                        count = minCount + 1,
                        count_offset = minCount,
                        random = Guid.NewGuid()
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
                    logger.LogInformation("Attempting to bulk add new custom {0}...", typeof(T).Name);
                    foreach (var valueb in value.Split(new string[] { "||" }, StringSplitOptions.RemoveEmptyEntries).Select(_ => trimmer(_)))
                    {
                        var valuec = await ReplaceImageURIs(key, valueb, logger);
                        if (!valid(valuec))
                        {
                            await ephemeralMessageCollector.AddEAsync(slashData, $"Not valid: `{key}` {valuec}");
                            break;
                        }
                        resp = new T
                        {
                            Id = Guid.NewGuid().ToString().Split('-')[0],
                            key = key,
                            value = valuec,
                            user_id = slashData.user_id,
                            count = minCount,
                            count_offset = minCount,
                            random = Guid.NewGuid()
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
                    logger.LogInformation("Retrieving list of all custom {0} for specified key...", typeof(T).Name);
                    var query = docClient.CreateDocumentQuery<T>(
                        C.CDB.DCUri,
                        "SELECT * FROM r",
                        new FeedOptions { PartitionKey = new PartitionKey($"{typeof(T).Name}|{key}") })
                        .AsDocumentQuery();
                    var results = (await query.GetAllResults(logger)).ToList();
                    results.Sort(
                        (r1, r2) =>
                        {
                            var countCompare = (r2.count - r2.count_offset).CompareTo(r1.count - r1.count_offset);
                            return countCompare == 0 ? r1.Id.CompareTo(r2.Id) : countCompare;
                        });
                    await ephemeralMessageCollector.AddEAsync(slashData, $"Key: `{key}` Values (where count is: real, adjusted):\n\n{string.Join("\n", results.Select(r => $"`{r.Id} ({r.count - r.count_offset:#,#}, {r.count:#,#})` {r.value}"))}");
                    break;

                case "remove":
                    try
                    {
                        logger.LogInformation("Attempting to remove existing record...");
                        await docClient.DeleteDocumentAsync(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, value), new RequestOptions { PartitionKey = new PartitionKey($"{typeof(T).Name}|{key}") });
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

        private static readonly Regex ShortURI = new Regex(@"((https?|ftp):)?\/\/[^\s\/$.?#].[^\s]*", RegexOptions.Compiled);
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

                    // Figure out the image extension
                    var imageExt = string.Empty;
                    var urlImageName = matchUri.Substring(matchUri.LastIndexOf('/'));
                    if (urlImageName.Contains(".")) imageExt = urlImageName.Substring(urlImageName.LastIndexOf('.'));

                    // Compute the hash of the image for the filename
                    string imageName;
                    using (var md5 = MD5.Create())
                    {
                        imageName = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
                        stream.Seek(0, System.IO.SeekOrigin.Begin);
                    }

                    // Upload and replace the blob
                    var blob = blobContainer.GetBlockBlobReference($"{key.Replace(' ', '-')}/{imageName}{imageExt}");
                    blob.Properties.ContentType = response.Content.Headers.ContentType.MediaType;
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

        private class WikiResponse
        {
            public WikiQuery query { get; set; }

            public class WikiQuery
            {
                public Dictionary<string, WikiQueryPage> pages { get; set; }

                public class WikiQueryPage
                {
                    public string title { get; set; }
                    public string extract { get; set; }
                    public DateTime touched { get; set; }
                    public string fullurl { get; set; }
                }
            }
        }
    }
}
