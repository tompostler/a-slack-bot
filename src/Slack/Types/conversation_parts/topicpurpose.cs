namespace Slack.Types.conversation_parts
{
    /// <summary>
    /// A  conversation object contains information about a channel-like thing in Slack. It might be a public channel,
    /// a private channel, a direct message, or a multi-person direct message.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/types/conversation on 2018-11-08
    /// </remarks>
    public class topicpurpose
    {
        public string value { get; set; }
        public string creator { get; set; }
        public long last_set { get; set; }
    }
}
