using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace a_slack_bot.Documents2
{
    public class IdMapping : Resource
    {
        [JsonIgnore]
        public static readonly PartitionKey PartitionKey = new PartitionKey(nameof(name));

        public override string Id => nameof(IdMapping);
        public string name { get; set; }
        public Dictionary<string, string> mapping { get; set; }
    }
}
