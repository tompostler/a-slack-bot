namespace Slack.Events.Inner.message_parts
{
    /// <summary>
    /// If the message has been edited after posting it will include an edited property, including the user ID of the
    /// editor, and the timestamp the edit happened. The original text of the message is not available.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/events/message on 2018-08-26
    /// </remarks>
    public class edited
    {
        /// <summary>
        /// The user ID of the editor
        /// </summary>
        public string user { get; set; }

        /// <summary>
        /// The timestamp the edit happened
        /// </summary>
        public string ts { get; set; }
    }
}
