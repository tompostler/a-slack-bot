using Microsoft.Azure.Documents.Client;
using System;
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
                public const string GamesBlackjack = "games-blackjack";
            }

            public static readonly IReadOnlyDictionary<string, Uri> CUs = new Dictionary<string, Uri>
            {
                [Col.GamesBlackjack] = UriFactory.CreateDocumentCollectionUri(DN, Col.GamesBlackjack)
            };
        }
    }
}
