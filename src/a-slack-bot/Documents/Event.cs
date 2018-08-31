namespace a_slack_bot.Documents
{
    public class Event : BaseDocument<Slack.Events.Inner.IEvent>
    {
        public override string Type => nameof(Event);
        public override string Subtype { get { return Content.type; } set { } }
    }
}
