using System;
using System.Configuration;
using System.Text;

namespace a_slack_bot
{
    public static class Settings
    {
        public static string CosmosDBConnection => ConfigurationManager.AppSettings.Get(nameof(CosmosDBConnection));

        // https://github.com/Azure/azure-documentdb-dotnet/issues/203
        public static Uri CosmosDBEndpoint => new Uri(ConfigurationManager.AppSettings.Get(nameof(CosmosDBEndpoint)));
        public static string CosmosDBKey => ConfigurationManager.AppSettings.Get(nameof(CosmosDBKey));

        public static bool Debug => bool.TryParse(ConfigurationManager.AppSettings.Get(nameof(Debug)), out bool t) && t;
        public static string SlackOauthToken => ConfigurationManager.AppSettings.Get(nameof(SlackOauthToken));
        public static string SlackOauthBotToken => ConfigurationManager.AppSettings.Get(nameof(SlackOauthBotToken));
        public static string SlackSigningSecret => ConfigurationManager.AppSettings.Get(nameof(SlackSigningSecret));
        public static byte[] SlackSigningSecretBytes => Encoding.ASCII.GetBytes(SlackSigningSecret);
    }
}
