namespace Slack.Events.Inner.message_parts.block_parts
{
    /// <summary>
    /// An object containing some text, formatted either as plain_text or using mrkdwn, our proprietary textual markup that's just different enough from Markdown to frustrate you.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/reference/block-kit/composition-objects#text on 2020-06-01
    /// </remarks>
    public class text_component
    {
        /// <summary>
        /// The formatting to use for this text object. Can be one of plain_textor mrkdwn.
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// The text for the block. This field accepts any of the standard text formatting markup when type is mrkdwn.
        /// </summary>
        public string text { get; set; }

        /// <summary>
        /// Indicates whether emojis in a text field should be escaped into the colon emoji format. This field is only usable when type is plain_text.
        /// </summary>
        public bool emoji { get; set; }

        /// <summary>
        /// When set to false (as is default) URLs will be auto-converted into links, conversation names will be link-ified, and certain mentions will be automatically parsed.
        /// Using a value of true will skip any preprocessing of this nature, although you can still include manual parsing strings. This field is only usable when type is mrkdwn.
        /// </summary>
        public bool verbatim { get; set; }
    }
}
