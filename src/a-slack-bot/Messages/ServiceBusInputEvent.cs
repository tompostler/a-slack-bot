namespace a_slack_bot.Messages
{
    public sealed class ServiceBusInputEvent
    {
        public Slack.Events.Inner.IEvent @event { get; set; }
    }
}
