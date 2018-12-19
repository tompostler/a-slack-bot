using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace a_slack_bot.Documents
{
    public class Standings : Base
    {
        [JsonIgnore]
        public static readonly Uri DocUri = UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, nameof(Standings));
        public override string doctype => nameof(Standings);

        public override string Id { get => nameof(Standings); set { } }

        public Dictionary<string, long> bals { get; set; } = new Dictionary<string, long>();
    }
}
