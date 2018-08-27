namespace Slack.Events.Outer
{
    /// <summary>
    /// Verifies ownership of an Events API Request URL
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/events/url_verification on 2018-08-26
    /// </remarks>
    public class url_verification : EventBase
    {
        public string challenge { get; set; }
    }
}
