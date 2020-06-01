namespace Slack.Events.Inner.message_parts
{
    /// <summary>
    /// Blocks are a series of components that can be combined to create visually rich and compellingly interactive messages.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/reference/block-kit/blocks on 2020-06-01
    /// </remarks>
    public class block
    {
        /// <summary>
        /// The type of block.
        /// </summary>
        /// <remarks>
        /// The full list of block types supported by the Events API is:
        /// section     A section is one of the most flexible blocks available - it can be used as a simple text block, in combination with text
        ///             fields, or side-by-side with any of the available block elements.
        /// divider     A content divider, like an <hr>, to split up different blocks inside of a message. The divider block is nice and neat,
        ///             requiring only a type.
        /// image       A simple image block, designed to make those cat photos really pop.
        /// actions     A block that is used to hold interactive elements.
        /// context     Displays message context, which can include both images and text.
        /// file        Displays a remote file.
        /// 
        /// </remarks>
        public string type { get; set; }

        /// <summary>
        /// A string acting as a unique identifier for a block. You can use this block_id when you receive an interaction payload to identify the
        /// source of the action. If not specified, one will be generated. Maximum length for this field is 255 characters. block_id should be unique
        /// for each message and each iteration of a message. If a message is updated, use a new block_id.
        /// </summary>
        public string block_id { get; set; }

        /// <summary>
        /// The text for the block, in the form of a text object. Maximum length for the text in this field is 3000 characters. This field is not
        /// required if a valid array of fields objects is provided instead.
        /// 
        /// Used in section blocks.
        /// </summary>
        public block_parts.text_component text { get; set; }

        //used in section blocks
        //public object accessory { get; set; }

        /// <summary>
        /// The URL of the image to be displayed. Maximum length for this field is 3000 characters.
        /// 
        /// Used in image blocks.
        /// </summary>
        public string image_url { get; set; }

        /// <summary>
        /// A plain-text summary of the image. This should not contain any markup. Maximum length for this field is 2000 characters.
        /// 
        /// Used in image blocks.
        /// </summary>
        public string alt_text { get; set; }

        /// <summary>
        /// An optional title for the image in the form of a text object that can only be of type: plain_text. Maximum length for the text in this
        /// field is 2000 characters.
        /// 
        /// Used in image blocks.
        /// </summary>
        public block_parts.text_component title { get; set; }

        //used in actions blocks
        //public object elements { get; set; }

        //used in context blocks
        //public object elements { get; set; }

        /// <summary>
        /// The external unique ID for this file.
        /// 
        /// Used in file blocks.
        /// </summary>
        public string external_id { get; set; }

        /// <summary>
        /// At the moment, source will always be remote for a remote file.
        /// 
        /// Used in file blocks.
        /// </summary>
        public string source { get; set; }
    }
}
