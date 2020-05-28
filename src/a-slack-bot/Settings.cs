using System;
using System.Configuration;
using System.Text;

namespace a_slack_bot
{
    public static class Settings
    {
        public static string AzureWebJobsServiceBus => ConfigurationManager.AppSettings.Get(nameof(AzureWebJobsServiceBus));

        public static string AzureWebJobsStorage => ConfigurationManager.AppSettings.Get(nameof(AzureWebJobsStorage));
        public static string BlobContainerName => ConfigurationManager.AppSettings.Get(nameof(BlobContainerName));
        public static string BlobsSourceHostname => ConfigurationManager.AppSettings.Get(nameof(BlobsSourceHostname));
        public static string BlobsTargetHostname => ConfigurationManager.AppSettings.Get(nameof(BlobsTargetHostname));

        public static string CosmosDBConnection => ConfigurationManager.AppSettings.Get(nameof(CosmosDBConnection));

        // https://github.com/Azure/azure-documentdb-dotnet/issues/203
        public static Uri CosmosDBEndpoint => new Uri(ConfigurationManager.AppSettings.Get(nameof(CosmosDBEndpoint)));
        public static string CosmosDBKey => ConfigurationManager.AppSettings.Get(nameof(CosmosDBKey));

        public static bool Debug => bool.TryParse(ConfigurationManager.AppSettings.Get(nameof(Debug)), out bool t) && t;
        public static string SlackAppID => ConfigurationManager.AppSettings.Get(nameof(SlackAppID));
        public static string SlackOauthToken => ConfigurationManager.AppSettings.Get(nameof(SlackOauthToken));
        public static string SlackOauthBotToken => ConfigurationManager.AppSettings.Get(nameof(SlackOauthBotToken));
        public static string SlackSigningSecret => ConfigurationManager.AppSettings.Get(nameof(SlackSigningSecret));
        public static byte[] SlackSigningSecretBytes => Encoding.ASCII.GetBytes(SlackSigningSecret);

        /// <summary>
        /// Harcoded settings for the one server. Figure out what to do with this in the future.
        /// </summary>
        public static class Runtime
        {
            public static string NotifyChannelId => "C3TUM94BX";
        }
    }
}
