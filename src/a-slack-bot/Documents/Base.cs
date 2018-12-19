using Microsoft.Azure.Documents;
using Newtonsoft.Json;

namespace a_slack_bot.Documents
{
    public abstract class Base : Resource
    {
        [JsonIgnore]
        public virtual PartitionKey PK => new PartitionKey(this.doctype);
        public abstract string doctype { get; }
    }
}
