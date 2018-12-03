using System.Collections.Generic;

namespace a_slack_bot
{
    public static class C
    {
        /// <summary>
        /// Cosmos DB
        /// </summary>
        public static class CDB
        {
            public const string DN = "aslackbot";
            public const string CN = "aslackbot";
            public const string P = "/" + nameof(Documents.BaseDocument.TypeSubtype);
            public const string CSS = nameof(Settings.CosmosDBConnection);
        }
        /// <summary>
        /// Cosmos DB
        /// </summary>
        public static class CDB2
        {
            public const string DN = "aslackbot2";

            /// <summary>
            /// Collections
            /// </summary>
            public static class Col
            {
                public const string CustomResponses = "custom-responses";
            }

            public static readonly IReadOnlyDictionary<string, string> PKs = new Dictionary<string, string>
            {
                [Col.CustomResponses] = nameof(Documents2.Response.key)
            };

            public const string CSS = nameof(Settings.CosmosDBConnection);
        }

        /// <summary>
        /// Service Bus Queues
        /// </summary>
        public static class SBQ
        {
            public const string Blackjack = "blackjack";

            public const string InputEvent = "input-event";
            public const string InputSlash = "input-slash";
            public const string InputThread = "input-thread";

            public const string SendMessage = "send-message";
            public const string SendMessageEphemeral = "send-message-ephemeral";
        }

        public static class Headers
        {
            public static class Slack
            {
                public const string RequestTimestamp = "X-Slack-Request-Timestamp";
                public const string Signature = "X-Slack-Signature";
            }
        }

        public static string VersionStr => $"{typeof(C).Assembly.ManifestModule.Name} v{GitVersionInformation.SemVer}+{GitVersionInformation.Sha.Substring(0, 6)}";
    }
}
