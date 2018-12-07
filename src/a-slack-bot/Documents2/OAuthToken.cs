using Microsoft.Azure.Documents;
using Newtonsoft.Json;

namespace a_slack_bot.Documents2
{
    public class OAuthToken : Resource
    {
        [JsonIgnore]
        public PartitionKey PK => new PartitionKey(this.type);

        public string type { get; set; }
        public string token { get; set; }
    }
}
