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

            public static readonly Uri DCUri = UriFactory.CreateDocumentCollectionUri(DN, CN);
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
                public const string GamesBlackjack = "games-blackjack";
                public const string IdMappings = "id-mappings";
                public const string SlackEvents = "slack-events";
                public const string SlackOAuthTokens = "slack-tokens";
                public const string Whitelists = "whitelists";
            }

            public static readonly IReadOnlyDictionary<string, Uri> CUs = new Dictionary<string, Uri>
            {
                [Col.CustomResponses] = UriFactory.CreateDocumentCollectionUri(DN, Col.CustomResponses),
                [Col.GamesBlackjack] = UriFactory.CreateDocumentCollectionUri(DN, Col.GamesBlackjack),
                [Col.IdMappings] = UriFactory.CreateDocumentCollectionUri(DN, Col.IdMappings),
                [Col.SlackEvents] = UriFactory.CreateDocumentCollectionUri(DN, Col.SlackEvents),
                [Col.SlackOAuthTokens] = UriFactory.CreateDocumentCollectionUri(DN, Col.SlackOAuthTokens),
                [Col.Whitelists] = UriFactory.CreateDocumentCollectionUri(DN, Col.Whitelists)
            };
        }
    }
}
