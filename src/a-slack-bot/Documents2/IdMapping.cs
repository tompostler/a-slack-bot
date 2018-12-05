using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace a_slack_bot.Documents2
{
    public class IdMapping : Resource
    {
        [JsonIgnore]
        public PartitionKey PK => new PartitionKey(this.name);

        public override string Id => nameof(IdMapping);
        public string name { get; set; }
        public Dictionary<string, string> mapping { get; set; }
    }
}
