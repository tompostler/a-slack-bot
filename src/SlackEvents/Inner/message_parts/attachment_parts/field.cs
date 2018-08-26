namespace Slack.Events.Inner.message_parts.attachment_parts
{
    /// <summary>
    /// Each field is defined as a dictionary with key-value pairs. Fields get displayed in a table-like way.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/docs/message-attachments on 2018-08-26
    /// </remarks>
    public class field
    {
        /// <summary>
        /// Shown as a bold heading above the value text. It cannot contain markup and will be escaped for you.
        /// </summary>
        public string title { get; set; }

        /// <summary>
        /// The text value of the field. It may contain standard message markup and must be escaped as normal. May be multi-line.
        /// </summary>
        public string value { get; set; }

        /// <summary>
        /// An optional flag indicating whether the value is short enough to be displayed side-by-side with other values.
        /// </summary>
        public bool @short { get; set; }
    }
}
