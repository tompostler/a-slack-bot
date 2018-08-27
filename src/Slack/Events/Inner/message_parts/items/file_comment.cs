namespace Slack.Events.Inner.message_parts.items
{
    /// <summary>
    /// See <see cref="Inner.file_comment"/>
    /// </summary>
    public class file_comment : ItemBase
    {
        public string File_comment { get; set; }
        public string file { get; set; }
    }
}
