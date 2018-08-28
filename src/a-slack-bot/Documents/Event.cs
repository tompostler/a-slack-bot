namespace a_slack_bot.Documents
{
    public class Event : IDocument<Slack.Events.Inner.IEvent>
    {
        public override string DocumentType => nameof(Event);
        public override string DocumentClass => Content.type;

        public override Slack.Events.Inner.IEvent Content { get; set; }
    }
}
