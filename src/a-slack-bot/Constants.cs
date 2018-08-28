namespace a_slack_bot
{
    public static class Constants
    {
        /// <summary>
        /// Cosmos DB
        /// </summary>
        public static class CDB
        {
            public const string DN = "aslackbot";
            public const string CN = "aslackbot";
            public const string CSS = nameof(Settings.CosmosDBConnection);
        }

        /// <summary>
        /// Service Bus Queues
        /// </summary>
        public static class SBQ
        {
            public const string InputEvent = "input-event";
            public const string InputSlash = "input-slash";

            public const string SendMessage = "send-message";
        }

        public static class Headers
        {
            public static class Slack
            {
                public const string RequestTimestamp = "X-Slack-Request-Timestamp";
                public const string Signature = "X-Slack-Signature";
            }
        }
    }
}
