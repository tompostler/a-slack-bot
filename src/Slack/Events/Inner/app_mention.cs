namespace Slack.Events.Inner
{
    /// <summary>
    /// Subscribe to only the message events that mention your app or bot
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/events/app_mention on 2018-08-29
    /// </remarks>
    public class app_mention : message
    {
        public app_mention()
        {
            this.type = nameof(app_mention);
        }
    }
}
