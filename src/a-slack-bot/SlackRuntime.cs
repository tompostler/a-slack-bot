using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace a_slack_bot
{
    /// <summary>
    /// Configuration pulled from Slack APIs at runtime. Horribly static object that requires an await Init before using.
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
        public static async Task Init(ILogger logger)
        {
            if (!Initialized)
            {
                await Lock.WaitAsync();
                if (!Initialized)
                    await InnerInit(logger);
                Lock.Release();
            }
        }
        private static async Task InnerInit(ILogger logger)
        {
            U = new SlackUsers();
            await U.Init(logger);

            Initialized = true;
        }
        public static void Deit() => Initialized = false;

        /// <summary>
        /// Users
        /// </summary>
        public static SlackUsers U { get; private set; }

        public class SlackUsers
        {
            public IReadOnlyCollection<Slack.Types.user> All => users.Values;
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

                IdToName = users.Values.ToDictionary(u => u.id, u => u.profile.display_name);
            }
        }
    }
}
