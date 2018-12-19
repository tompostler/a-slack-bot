namespace a_slack_bot.Documents
{
    public class Event : Base
    {
        public override string doctype => nameof(Event);

        public override string Id { get => this.@event.event_ts; set { } }
        public Slack.Events.Inner.IEvent @event { get; set; }
    }
}
