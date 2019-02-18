namespace a_slack_bot.Messages
{
    /// <summary>
    /// https://api.slack.com/methods/reactions.add
    /// </summary>
    public sealed class ServiceBusReactionAdd
    {
        /// <summary>
        /// Reaction (emoji) name.
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Channel where the message to add reaction to was posted.
        /// </summary>
        public string channel { get; set; }

        /// <summary>
        /// Timestamp of the message to add reaction to.
        /// </summary>
        public string timestamp { get; set; }
    }
}
