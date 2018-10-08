namespace a_slack_bot.Documents
{
    public class Event : BaseDocument<Slack.Events.Inner.IEvent>
    {
        public override string Id { get => this.Content.event_ts; set { } }
        public override string Type => nameof(Event);
        public override string Subtype { get => this.Content.type; set { } }
    }
}
