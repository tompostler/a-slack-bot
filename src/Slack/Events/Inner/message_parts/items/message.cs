namespace Slack.Events.Inner.message_parts.items
{
    /// <summary>
    /// See <see cref="Inner.message"/>
    /// </summary>
    public class message : ItemBase
    {
        public string channel { get; set; }
        public string ts { get; set; }
    }
}
