using Microsoft.Azure.Documents;
using Newtonsoft.Json;

namespace a_slack_bot.Documents2
{
    public class Event : Resource
    {
        [JsonIgnore]
        public PartitionKey PK => new PartitionKey(this.type);

        public override string Id { get => this.@event.event_ts; set { } }
        public string type { get => this.@event.type; set { } }
        public Slack.Events.Inner.IEvent @event { get; set; }
    }
}
