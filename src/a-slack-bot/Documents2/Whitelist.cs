using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace a_slack_bot.Documents2
{
    public class Whitelist : Resource
    {
        [JsonIgnore]
        public PartitionKey PK => new PartitionKey(this.type);

        public string type { get; set; }
        public HashSet<string> values { get; set; }
    }
}
