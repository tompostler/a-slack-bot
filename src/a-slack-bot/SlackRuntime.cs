using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace a_slack_bot
{
    /// <summary>
    /// Configuration pulled from Slack APIs or Cosmos DB at runtime. Horribly static object that requires an await Init before using.
    /// </summary>
    public static class SR
    {
        private static HttpClient httpClient = new HttpClient();
        static SR()
        {
            if (!string.IsNullOrWhiteSpace(Settings.SlackOauthBotToken))
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.SlackOauthBotToken);

            // This would change to a setting on the DocumentClient in DocumentDB SDK 1.15+
            // This change _shouldn't_ break anything.
            Newtonsoft.Json.JsonConvert.DefaultSettings = () => new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore };
        }

        private static SemaphoreSlim Lock = new SemaphoreSlim(1);
        private static bool Initialized = false;
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
            R = new SlackResponses();
            T = new SlackTokens();
            U = new SlackUsers();
            W = new SlackWhitelist();
            var docClient = new DocumentClient(Settings.CosmosDBEndpoint, Settings.CosmosDBKey);
            await Task.WhenAll(new[] {
                C.Init(logger, docClient),
                R.Init(logger, docClient),
                T.Init(logger, docClient),
                U.Init(logger, docClient),
                W.Init(logger, docClient),
            });

            InitializedDuration = Stopwatch.StartNew();
            Initialized = true;
        }
        public static void Deit() => Initialized = false;

        public static SlackConversations C { get; private set; }

        /// <summary>
        /// Responses
        /// </summary>
        public static SlackResponses R { get; private set; }

        /// <summary>
        /// Shared static random.
        /// </summary>
        public static Random Rand { get; } = new Random();

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
            public int MaxNameLength { get; private set; }
            public IReadOnlyDictionary<string, Slack.Types.converation> IdToUser => this.conversations;

            private Dictionary<string, Slack.Types.converation> conversations { get; set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var response = await httpClient.GetAsync("https://slack.com/api/conversations.list");
                var conversationResponse = await response.Content.ReadAsAsync<Slack.WebAPIResponse>();
                if (!conversationResponse.ok)
                {
                    logger.LogError("Not ok when trying to fetch users! Warning:'{0}' Error:'{1}'", conversationResponse.warning, conversationResponse.error);
                    throw new Exception($"Bad {nameof(SlackConversations)}.{nameof(Init)}");
                }

                this.conversations = conversationResponse.channels.ToDictionary(_ => _.id);
                logger.LogInformation("Populated {0} conversations.", this.conversations.Count);
                if (Settings.Debug)
                    logger.LogInformation("Display names: '{0}'", string.Join("','", this.conversations.Values.Select(u => u.name)));

                var idToNameDict = this.conversations.Values.ToDictionary(u => u.id, u => u.name);
                this.IdToName = idToNameDict;
                this.MaxNameLength = this.IdToName.Values.Max(un => un.Length);

                var converationMapDoc = new Documents.IdMapping
                {
                    Id = nameof(SlackConversations),
                    Subtype = nameof(SR),
                    Content = idToNameDict
                };
                await docClient.UpsertDocumentAsync(
                    UriFactory.CreateDocumentCollectionUri(a_slack_bot.C.CDB.DN, a_slack_bot.C.CDB.CN),
                    converationMapDoc,
                    new RequestOptions { PartitionKey = new PartitionKey(converationMapDoc.TypeSubtype) },
                    disableAutomaticIdGeneration: true);
            }
        }

        public class SlackResponses
        {
            public HashSet<string> Keys { get; private set; }
            public Dictionary<string, Dictionary<string, string>> AllResponses { get; private set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var docQuery = docClient.CreateDocumentQuery<Documents.Response>(
                    UriFactory.CreateDocumentCollectionUri(a_slack_bot.C.CDB.DN, a_slack_bot.C.CDB.CN),
                    $"SELECT * FROM r WHERE r.{nameof(Documents.BaseDocument.Type)} = '{nameof(Documents.Response)}' AND r.id <> '{nameof(Documents.ResponsesUsed)}'",
                    new FeedOptions { EnableCrossPartitionQuery = true })
                    .AsDocumentQuery();

                var responses = await docQuery.GetAllResults(logger);

                this.AllResponses = new Dictionary<string, Dictionary<string, string>>();
                foreach (var response in responses)
                {
                    if (!this.AllResponses.ContainsKey(response.Subtype))
                        this.AllResponses.Add(response.Subtype, new Dictionary<string, string>());
                    this.AllResponses[response.Subtype].Add(response.Id, response.Content);
                }
                this.Keys = new HashSet<string>(this.AllResponses.Keys);
            }
        }

        public class SlackTokens
        {
            public IReadOnlyDictionary<string, string> ChatWriteUser { get; private set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var docQuery = docClient.CreateDocumentQuery<Documents.OAuthToken>(
                    UriFactory.CreateDocumentCollectionUri(a_slack_bot.C.CDB.DN, a_slack_bot.C.CDB.CN),
                    new FeedOptions { PartitionKey = new PartitionKey(nameof(Documents.OAuthToken) + "|user") })
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
            public int MaxNameLength { get; private set; }
            public IReadOnlyDictionary<string, Slack.Types.user> IdToUser => this.users;

            private Dictionary<string, Slack.Types.user> users { get; set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var response = await httpClient.GetAsync("https://slack.com/api/users.list");
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
                this.MaxNameLength = this.IdToName.Values.Max(un => un.Length);

                var userMapDoc = new Documents.IdMapping
                {
                    Id = nameof(SlackUsers),
                    Subtype = nameof(SR),
                    Content = idToNameDict
                };
                await docClient.UpsertDocumentAsync(
                    UriFactory.CreateDocumentCollectionUri(a_slack_bot.C.CDB.DN, a_slack_bot.C.CDB.CN),
                    userMapDoc,
                    new RequestOptions { PartitionKey = new PartitionKey(userMapDoc.TypeSubtype) },
                    disableAutomaticIdGeneration: true);
            }
        }

        public class SlackWhitelist
        {
            public IReadOnlyDictionary<string, HashSet<string>> CommandsChannels { get; private set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var docQuery = docClient.CreateDocumentQuery<Documents.Whitelist>(
                    UriFactory.CreateDocumentCollectionUri(a_slack_bot.C.CDB.DN, a_slack_bot.C.CDB.CN),
                    new FeedOptions { PartitionKey = new PartitionKey(nameof(Documents.Whitelist) + "|command") })
                    .AsDocumentQuery();

                var tokens = await docQuery.GetAllResults(logger);

                this.CommandsChannels = tokens.ToDictionary(t => t.Id, t => t.values);
            }
        }
    }
}
