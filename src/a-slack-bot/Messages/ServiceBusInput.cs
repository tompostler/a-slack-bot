namespace a_slack_bot.Messages
{
    public sealed class ServiceBusInput
    {
        public Slack.Events.Inner.IEvent @event { get; set; }
    }
}
