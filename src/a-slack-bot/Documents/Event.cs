using Microsoft.Azure.Documents;

namespace a_slack_bot.Documents
{
    public class Event : Document, IDocument<Slack.Events.Inner.IEvent>
    {
        public string DocType => nameof(Event);
        public string DocSubtype => Content.type;

        public Slack.Events.Inner.IEvent Content { get; set; }
    }
}
