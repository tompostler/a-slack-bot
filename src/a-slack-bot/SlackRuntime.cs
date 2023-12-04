using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace a_slack_bot
{
    /// <summary>
    /// Configuration pulled from Slack APIs or Cosmos DB at runtime. Horribly static object that requires an await Init before using.
    /// </summary>
    public static class SR
    {
        private static readonly HttpClient appHttpClient = new HttpClient();
        private static readonly HttpClient botHttpClient = new HttpClient();
        static SR()
        {
            if (!string.IsNullOrWhiteSpace(Settings.SlackOauthToken))
                appHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.SlackOauthToken);
            if (!string.IsNullOrWhiteSpace(Settings.SlackOauthBotToken))
                botHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.SlackOauthBotToken);

            // This would change to a setting on the DocumentClient in DocumentDB SDK 1.15+
            // This change _shouldn't_ break anything.
            Newtonsoft.Json.JsonConvert.DefaultSettings = () => new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore };
        }

        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1);
        private static bool Initialized = false;
        private static bool OneTimeInitialized = false;
        private static Stopwatch InitializedDuration;
        public static async Task Init(ILogger logger)
        {
            if (!Initialized)
            {
                await Lock.WaitAsync();
                if (!Initialized)
                    await InnerInit(logger);
                Lock.Release();
            }
            // To make sure stale data doesn't stick around forever, deit at least every 11 minutes
            else if (InitializedDuration.Elapsed > TimeSpan.FromMinutes(11))
                Deit();
        }
        private static async Task InnerInit(ILogger logger)
        {
            C = new SlackConversations();
            E = new SlackEmojis();
            Ra = new SlackReactions();
            Re = new SlackResponses();
            T = new SlackTokens();
            U = new SlackUsers();
            W = new SlackWhitelist();
            using (var docClient = new DocumentClient(Settings.CosmosDBEndpoint, Settings.CosmosDBKey))
            {
                if (!OneTimeInitialized)
                {
                    await InnerOneTimeInit(logger, docClient);
                    OneTimeInitialized = true;
                }
                await Task.WhenAll(new[] {
                    C.Init(logger, docClient),
                    E.Init(logger),
                    Ra.Init(logger, docClient),
                    Re.Init(logger, docClient),
                    T.Init(logger, docClient),
                    U.Init(logger, docClient),
                    W.Init(logger, docClient),
                });
            }

            InitializedDuration = Stopwatch.StartNew();
            Initialized = true;
        }
        private static async Task InnerOneTimeInit(ILogger logger, DocumentClient docClient)
        {
            var dbResponse = await docClient.CreateDatabaseIfNotExistsAsync(new Database { Id = a_slack_bot.C.CDB.DN }, new RequestOptions { OfferThroughput = 400 });
            logger.LogInformation("DB: {0}", dbResponse.StatusCode);

            var colResponse = await docClient.CreateDocumentCollectionIfNotExistsAsync(
                dbResponse.Resource.SelfLink,
                new DocumentCollection
                {
                    Id = a_slack_bot.C.CDB.CN,
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Paths = new System.Collections.ObjectModel.Collection<string>
                        {
                            "/" + nameof(Documents.Base.doctype)
                        }
                    }
                });
            logger.LogInformation("Col {0}: {1}", a_slack_bot.C.CDB.CN, colResponse.StatusCode);

            try
            {
                await docClient.ReadStoredProcedureAsync(UriFactory.CreateStoredProcedureUri(a_slack_bot.C.CDB.DN, a_slack_bot.C.CDB.CN, a_slack_bot.C.CDB.SP.rething_count2));
            }
            catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("Sproc {0}: {1}", a_slack_bot.C.CDB.SP.rething_count2, dce);
                try
                {
                    var sprocResponse = await docClient.CreateStoredProcedureAsync(
                        colResponse.Resource.SelfLink,
                        new StoredProcedure
                        {
                            Id = a_slack_bot.C.CDB.SP.rething_count2,
                            Body = await new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream($"{nameof(a_slack_bot)}.StoredProcs.{a_slack_bot.C.CDB.SP.rething_count2}.js")).ReadToEndAsync()
                        });
                    logger.LogInformation("Sproc {0}: {1}", a_slack_bot.C.CDB.SP.rething_count2, sprocResponse.StatusCode);
                }
                catch (DocumentClientException dce2) when (dce2.StatusCode == HttpStatusCode.Conflict)
                {
                    logger.LogWarning("Conflict on sproc creation: {0}", dce2);
                }
            }
        }
        public static void Deit() => Initialized = false;

        /// <summary>
        /// Conversations
        /// </summary>
        public static SlackConversations C { get; private set; }

        /// <summary>
        /// Emoji
        /// </summary>
        public static SlackEmojis E { get; private set; }

        /// <summary>
        /// Reactions
        /// </summary>
        public static SlackReactions Ra { get; private set; }

        /// <summary>
        /// Responses
        /// </summary>
        public static SlackResponses Re { get; private set; }

        /// <summary>
        /// Shared static random.
        /// </summary>
        public static Random Rand { get; } = new Random();

        /// <summary>
        /// Shared static secure random.
        /// </summary>
        public static RNGCryptoServiceProvider RNGCSP { get; } = new RNGCryptoServiceProvider();

        /// <summary>
        /// Tokens
        /// </summary>
        public static SlackTokens T { get; private set; }

        /// <summary>
        /// Users
        /// </summary>
        public static SlackUsers U { get; private set; }

        /// <summary>
        /// Whitelist
        /// </summary>
        public static SlackWhitelist W { get; private set; }

        public class SlackConversations
        {
            public IReadOnlyCollection<Slack.Types.converation> All => this.conversations.Values;
            public IReadOnlyDictionary<string, string> IdToName { get; private set; }
            public IReadOnlyDictionary<string, string> LowerNameToId { get; private set; }
            public int MaxNameLength { get; private set; }
            public IReadOnlyDictionary<string, Slack.Types.converation> IdToConversation => this.conversations;

            private Dictionary<string, Slack.Types.converation> conversations { get; set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var response = await botHttpClient.GetAsync("https://slack.com/api/conversations.list?types=public_channel,private_channel,mpim,im");
                var conversationResponse = await response.Content.ReadAsAsync<Slack.WebAPIResponse>();
                if (!conversationResponse.ok)
                {
                    logger.LogError("Not ok when trying to fetch conversations! Warning:'{0}' Error:'{1}'", conversationResponse.warning, conversationResponse.error);
                    throw new Exception($"Bad {nameof(SlackConversations)}.{nameof(Init)}");
                }

                this.conversations = conversationResponse.channels.ToDictionary(_ => _.id);
                logger.LogInformation("Populated {0} conversations.", this.conversations.Count);
                if (Settings.Debug)
                    logger.LogInformation("Display names: '{0}'", string.Join("','", this.conversations.Values.Select(u => u.name)));

                var idToNameDict = this.conversations.Values.ToDictionary(u => u.id, u => u.name ?? u.user);
                this.IdToName = idToNameDict;
                this.LowerNameToId = this.IdToName.ToDictionary(u => u.Value.ToLower(), u => u.Key);
                this.MaxNameLength = this.IdToName.Values.Max(un => un.Length);

                var converationMapDoc = new Documents.IdMapping
                {
                    Id = nameof(SlackConversations),
                    mapping = idToNameDict
                };
                await docClient.UpsertDocumentAsync(
                    a_slack_bot.C.CDB.DCUri,
                    converationMapDoc,
                    new RequestOptions { PartitionKey = converationMapDoc.PK },
                    disableAutomaticIdGeneration: true);
            }
        }

        public class SlackEmojis
        {
            public HashSet<string> All { get; private set; }

            public async Task Init(ILogger logger)
            {
                var response = await appHttpClient.GetAsync("https://slack.com/api/emoji.list");
                var emojiResponse = await response.Content.ReadAsAsync<Slack.WebAPIResponse>();
                if (!emojiResponse.ok)
                {
                    logger.LogError("Not ok when trying to fetch emoji! Warning:'{0}' Error:'{1}'", emojiResponse.warning, emojiResponse.error);
                    throw new Exception($"Bad {nameof(SlackEmojis)}.{nameof(Init)}");
                }

                this.All = new HashSet<string>(emojiResponse.emoji.Keys.Union(a_slack_bot.C.Slack.SpaceSeparatedDefaultEmoji.Split(' ')).Union(a_slack_bot.C.Slack.SpaceSeparatedAdditionalEmoji.Split(' ')));
                logger.LogInformation("Populated {0} emojis, {1} of which are custom.", this.All.Count, emojiResponse.emoji.Count);
            }
        }

        public class SlackReactions
        {
            public HashSet<string> Keys { get; private set; }
            public Dictionary<string, Dictionary<string, string>> AllReactions { get; private set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var docQuery = docClient.CreateDocumentQuery<Documents.Reaction>(
                    a_slack_bot.C.CDB.DCUri,
                    $"SELECT * FROM r WHERE STARTSWITH(r.{nameof(Documents.Base.doctype)}, '{nameof(Documents.Reaction)}|')",
                    new FeedOptions { EnableCrossPartitionQuery = true })
                    .AsDocumentQuery();

                var reactions = await docQuery.GetAllResults(logger);

                this.AllReactions = new Dictionary<string, Dictionary<string, string>>();
                foreach (var reaction in reactions)
                {
                    if (!this.AllReactions.ContainsKey(reaction.key))
                        this.AllReactions.Add(reaction.key, new Dictionary<string, string>());
                    this.AllReactions[reaction.key].Add(reaction.Id, reaction.value);
                }
                this.Keys = new HashSet<string>(this.AllReactions.Keys);
            }
        }

        public class SlackResponses
        {
            public HashSet<string> Keys { get; private set; }
            public Dictionary<string, Dictionary<string, string>> AllResponses { get; private set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var docQuery = docClient.CreateDocumentQuery<Documents.Response>(
                    a_slack_bot.C.CDB.DCUri,
                    $"SELECT * FROM r WHERE STARTSWITH(r.{nameof(Documents.Base.doctype)}, '{nameof(Documents.Response)}|')",
                    new FeedOptions { EnableCrossPartitionQuery = true })
                    .AsDocumentQuery();

                var responses = await docQuery.GetAllResults(logger);

                this.AllResponses = new Dictionary<string, Dictionary<string, string>>();
                foreach (var response in responses)
                {
                    if (!this.AllResponses.ContainsKey(response.key))
                        this.AllResponses.Add(response.key, new Dictionary<string, string>());
                    this.AllResponses[response.key].Add(response.Id, response.value);
                }
                this.Keys = new HashSet<string>(this.AllResponses.Keys);
            }
        }

        public class SlackTokens
        {
            public IReadOnlyDictionary<string, string> ChatWriteUser { get; private set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var docQuery = docClient.CreateDocumentQuery<Documents.OAuthUserToken>(
                    a_slack_bot.C.CDB.DCUri,
                    new FeedOptions { PartitionKey = new Documents.OAuthUserToken().PK })
                    .AsDocumentQuery();

                var tokens = await docQuery.GetAllResults(logger);

                this.ChatWriteUser = tokens.ToDictionary(t => t.Id, t => t.token);
            }
        }

        public class SlackUsers
        {
            public IReadOnlyCollection<Slack.Types.user> All => this.users.Values;
            public Slack.Types.user BotUser { get; private set; }
            public IReadOnlyDictionary<string, string> IdToName { get; private set; }
            public IReadOnlyDictionary<string, string> LowerNameToId { get; private set; }
            public int MaxNameLength { get; private set; }
            public IReadOnlyDictionary<string, Slack.Types.user> IdToUser => this.users;

            private Dictionary<string, Slack.Types.user> users { get; set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var response = await botHttpClient.GetAsync("https://slack.com/api/users.list");
                var userResponse = await response.Content.ReadAsAsync<Slack.WebAPIResponse>();
                if (!userResponse.ok)
                {
                    logger.LogError("Not ok when trying to fetch users! Warning:'{0}' Error:'{1}'", userResponse.warning, userResponse.error);
                    throw new Exception($"Bad {nameof(SlackUsers)}.{nameof(Init)}");
                }

                this.users = userResponse.members.ToDictionary(_ => _.id);
                logger.LogInformation("Populated {0} users.", this.users.Count);
                if (Settings.Debug)
                    logger.LogInformation("Display names: '{0}'", string.Join("','", this.users.Values.Select(u => u.profile.display_name)));

                this.BotUser = this.users.Values.Single(u => u.profile.api_app_id == Settings.SlackAppID);

                var idToNameDict = this.users.Values.ToDictionary(u => u.id, u => string.IsNullOrEmpty(u.profile.display_name) ? u.profile.real_name : u.profile.display_name);
                this.IdToName = idToNameDict;
                this.LowerNameToId = this.IdToName.Where(u => u.Value != "deactivateduser").ToDictionary(u => u.Value.ToLower(), u => u.Key);
                this.MaxNameLength = this.IdToName.Values.Max(un => un.Length);

                var userMapDoc = new Documents.IdMapping
                {
                    Id = nameof(SlackUsers),
                    mapping = idToNameDict
                };
                await docClient.UpsertDocumentAsync(
                    a_slack_bot.C.CDB.DCUri,
                    userMapDoc,
                    new RequestOptions { PartitionKey = userMapDoc.PK },
                    disableAutomaticIdGeneration: true);
            }
        }

        public class SlackWhitelist
        {
            public IReadOnlyDictionary<string, HashSet<string>> CommandsChannels { get; private set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var docQuery = docClient.CreateDocumentQuery<Documents.Whitelist>(
                    a_slack_bot.C.CDB.DCUri,
                    new FeedOptions { PartitionKey = new Documents.Whitelist().PK })
                    .AsDocumentQuery();

                var whitelists = await docQuery.GetAllResults(logger);

                this.CommandsChannels = whitelists.FirstOrDefault(w => w.Id == "command")?.values ?? new Dictionary<string, HashSet<string>>();
            }
        }

        private static readonly Regex ConversationId = new Regex(@"<#(?<id>\w+)(?>\|[a-z0-9_-]+)?>", RegexOptions.Compiled);
        private static readonly Regex UserId = new Regex(@"<@(?<id>\w+)(?>\|[\w_-]+)?>", RegexOptions.Compiled);
        public static string MessageToPlainText(string messageText)
        {
            var matches = ConversationId.Matches(messageText);
            for (int i = 0; i < matches.Count; i++)
                if (SR.C.IdToName.ContainsKey(matches[i].Groups["id"].Value))
                    messageText = messageText.Replace(matches[i].Value, '#' + SR.C.IdToName[matches[i].Groups["id"].Value]);
            matches = UserId.Matches(messageText);
            for (int i = 0; i < matches.Count; i++)
                if (SR.U.IdToName.ContainsKey(matches[i].Groups["id"].Value))
                    messageText = messageText.Replace(matches[i].Value, '@' + SR.U.IdToName[matches[i].Groups["id"].Value]);
            // Remove any remaining special characters (URLs, etc)
            messageText = messageText.Replace("<", string.Empty).Replace(">", string.Empty);
            // Put back the escaped chars
            messageText = messageText.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");

            return messageText;
        }

        private static readonly Regex Conversation = new Regex(@"#(?<id>[a-z0-9_-]+)\b", RegexOptions.Compiled);
        private static readonly Regex User = new Regex(@"@(?<id>\w+)\b", RegexOptions.Compiled);
        public static string MessageSlackEncode(string messageText)
        {
            // Put back the escaped chars
            messageText = messageText.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

            var matches = Conversation.Matches(messageText);
            for (int i = 0; i < matches.Count; i++)
                if (SR.C.LowerNameToId.ContainsKey(matches[i].Groups["id"].Value.ToLower()))
                    messageText = messageText.Replace(matches[i].Value, $"<#{SR.C.LowerNameToId[matches[i].Groups["id"].Value.ToLower()]}>");
            matches = User.Matches(messageText);
            for (int i = 0; i < matches.Count; i++)
                if (SR.U.LowerNameToId.ContainsKey(matches[i].Groups["id"].Value.ToLower()))
                    messageText = messageText.Replace(matches[i].Value, $"<@{SR.U.LowerNameToId[matches[i].Groups["id"].Value.ToLower()]}>");

            return messageText;
        }
    }
}
