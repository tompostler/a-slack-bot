namespace a_slack_bot.Documents
{
    public class Event : BaseDocument
    {
        public Slack.Events.Inner.IEvent Content { get; set; }
    }

    public static class EventExtensions
    {
        public static Event ToDoc(this Slack.Events.Inner.IEvent @this)
        {
            var doc = new Event();
            doc.DocType = nameof(Event);
            doc.DocSubtype = @this.type;
            doc.Content = @this;
            return doc;
        }
    }
}
