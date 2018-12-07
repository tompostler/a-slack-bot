namespace upgrade._181206
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
                public const string SlackEvents = "slack-events";
                public const string SlackOAuthTokens = "slack-tokens";
            }
        }
    }
}
