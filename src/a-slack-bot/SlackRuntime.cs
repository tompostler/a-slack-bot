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
            R = new SlackResponses();
            T = new SlackTokens();
            U = new SlackUsers();
            W = new SlackWhitelist();
            var docClient = new DocumentClient(Settings.CosmosDBEndpoint, Settings.CosmosDBKey);
            await Task.WhenAll(new[] {
                R.Init(logger, docClient),
                T.Init(logger, docClient),
                U.Init(logger),
                W.Init(logger, docClient),
            });

            InitializedDuration = Stopwatch.StartNew();
            Initialized = true;
        }
        public static void Deit() => Initialized = false;

        /// <summary>
        /// Responses
        /// </summary>
        public static SlackResponses R { get; private set; }

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

        public class SlackResponses
        {
            public IReadOnlyCollection<string> Keys { get; private set; }
            public IReadOnlyDictionary<string, IReadOnlyCollection<string>> esponses { get; private set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var docQuery = docClient.CreateDocumentQuery<Documents.Response>(
                    UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                    new FeedOptions { EnableCrossPartitionQuery = true })
                    .AsDocumentQuery();

                var responses = await docQuery.GetAllResults(logger);

                var _esponses = new Dictionary<string, HashSet<string>>();
                foreach (var response in responses)
                {
                    if (!_esponses.ContainsKey(response.key))
                        _esponses.Add(response.key, new HashSet<string>());
                    _esponses[response.key].Add(response.value);
                }
                esponses = (IReadOnlyDictionary<string, IReadOnlyCollection<string>>)_esponses;
                Keys = (IReadOnlyCollection<string>)esponses.Keys;
            }
        }

        public class SlackTokens
        {
            public IReadOnlyDictionary<string, string> ChatWriteUser { get; private set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var docQuery = docClient.CreateDocumentQuery<Documents.OAuthToken>(
                    UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                    new FeedOptions { PartitionKey = new PartitionKey(nameof(Documents.OAuthToken) + "|user") })
                    .AsDocumentQuery();

                var tokens = await docQuery.GetAllResults(logger);

                ChatWriteUser = tokens.ToDictionary(t => t.Id, t => t.token);
            }
        }

        public class SlackUsers
        {
            public IReadOnlyCollection<Slack.Types.user> All => users.Values;
            public Slack.Types.user BotUser { get; private set; }
            public IReadOnlyDictionary<string, string> IdToName { get; private set; }
            public IReadOnlyDictionary<string, Slack.Types.user> IdToUser => users;

            private Dictionary<string, Slack.Types.user> users { get; set; }

            public async Task Init(ILogger logger)
            {
                var response = await httpClient.GetAsync("https://slack.com/api/users.list");
                var userResponse = await response.Content.ReadAsAsync<Slack.WebAPIResponse>();
                if (!userResponse.ok)
                {
                    logger.LogError("Not ok when trying to fetch users! Warning:'{0}' Error:'{1}'", userResponse.warning, userResponse.error);
                    throw new Exception($"Bad {nameof(SlackUsers)}.{nameof(Init)}");
                }

                users = userResponse.members.ToDictionary(_ => _.id);
                logger.LogInformation("Populated {0} users.", users.Count);
                if (Settings.Debug)
                    logger.LogInformation("Display names: '{0}'", string.Join("','", users.Values.Select(u => u.profile.display_name)));

                BotUser = users.Values.Single(u => u.profile.api_app_id == Settings.SlackAppID);

                IdToName = users.Values.ToDictionary(u => u.id, u => u.profile.display_name);
            }
        }

        public class SlackWhitelist
        {
            public IReadOnlyDictionary<string, HashSet<string>> CommandsChannels { get; private set; }

            public async Task Init(ILogger logger, DocumentClient docClient)
            {
                var docQuery = docClient.CreateDocumentQuery<Documents.Whitelist>(
                    UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                    new FeedOptions { PartitionKey = new PartitionKey(nameof(Documents.Whitelist) + "|command") })
                    .AsDocumentQuery();

                var tokens = await docQuery.GetAllResults(logger);

                CommandsChannels = tokens.ToDictionary(t => t.Id, t => t.values);
            }
        }
    }
}
