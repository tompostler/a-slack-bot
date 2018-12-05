using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using System;

namespace a_slack_bot.Documents2
{
    public class Response : Resource
    {
        [JsonIgnore]
        public PartitionKey PK => new PartitionKey(this.key);

        public string key { get; set; }
        public string value { get; set; }
        public string user_id { get; set; }

        public int count { get; set; }
        public Guid random { get; set; }
    }
}
