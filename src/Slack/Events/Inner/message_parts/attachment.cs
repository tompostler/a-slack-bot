using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace Slack.Events.Inner.message_parts
{
    /// <summary>
    /// Attachments let you add more context to a message, making them more useful and effective.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/docs/message-attachments on 2018-08-26
    /// </remarks>
    public class attachment
    {
        /// <summary>
        /// A plain-text summary of the attachment. This text will be used in clients that don't show formatted text
        /// (eg. IRC, mobile notifications) and should not contain any markup.
        /// </summary>
        public string fallback { get; set; }

        /// <summary>
        /// Like traffic signals, color-coding messages can quickly communicate intent and help separate them from the
        /// flow of other messages in the timeline.
        /// 
        /// An optional value that can either be one of good, warning, danger, or any hex color code(eg. #439FE0). This
        /// value is used to color the border along the left side of the message attachment.
        /// </summary>
        public string color { get; set; }

        /// <summary>
        /// This is optional text that appears above the message attachment block.
        /// </summary>
        public string pretext { get; set; }

        // The author parameters will display a small section at the top of a message attachment that can contain the following fields:

        /// <summary>
        /// Small text used to display the author's name.
        /// </summary>
        public string author_name { get; set; }

        /// <summary>
        /// A valid URL that will hyperlink the <see cref="author_name"/> text mentioned above. Will only work if
        /// <see cref="author_name"/> is present.
        /// </summary>
        public Uri author_link { get; set; }

        /// <summary>
        /// A valid URL that displays a small 16x16px image to the left of the <see cref="author_name"/> text. Will
        /// only work if <see cref="author_name"/> is present.
        /// </summary>
        public Uri author_icon { get; set; }

        /// <summary>
        /// Displayed as larger, bold text near the top of a message attachment.
        /// </summary>
        public string title { get; set; }

        /// <summary>
        /// By passing a valid URL in this parameter (optional), the <see cref="title"/> text will be hyperlinked.
        /// </summary>
        public Uri title_link { get; set; }

        /// <summary>
        /// This is the main text in a message attachment, and can contain standard message markup. The content will
        /// automatically collapse if it contains 700+ characters or 5+ linebreaks, and will display a "Show more..."
        /// link to expand the content. Links posted in the text field will not unfurl.
        /// </summary>
        public string text { get; set; }

        /// <summary>
        /// Fields are defined as an array. Each entry in the array is a single field. Each field is defined as a
        /// dictionary with key-value pairs. Fields get displayed in a table-like way.
        /// 
        /// For best results, include no more than 2-3 key/value pairs. There is no optimal, programmatic way to
        /// display a greater amount of tabular data on Slack today.
        /// </summary>
        public List<attachment_parts.field> fields { get; set; }

        /// <summary>
        /// A valid URL to an image file that will be displayed inside a message attachment. We currently support the
        /// following formats: GIF, JPEG, PNG, and BMP.
        /// 
        /// Large images will be resized to a maximum width of 360px or a maximum height of 500px, while still
        /// maintaining the original aspect ratio.
        /// </summary>
        public Uri image_url { get; set; }

        /// <summary>
        /// A valid URL to an image file that will be displayed as a thumbnail on the right side of a message
        /// attachment. We currently support the following formats: GIF, JPEG, PNG, and BMP.
        /// 
        /// The thumbnail's longest dimension will be scaled down to 75px while maintaining the aspect ratio of the
        /// image. The filesize of the image must also be less than 500 KB.
        /// 
        /// For best results, please use images that are already 75px by 75px.
        /// </summary>
        public Uri thumb_url { get; set; }

        /// <summary>
        /// Add some brief text to help contextualize and identify an attachment. Limited to 300 characters, and may be
        /// truncated further when displayed to users in environments with limited screen real estate.
        /// </summary>
        public string footer { get; set; }

        /// <summary>
        /// To render a small icon beside your footer text, provide a publicly accessible URL string in the footer_icon
        /// field. You must also provide a <see cref="footer"/> for the field to be recognized.
        /// </summary>
        public Uri footer_icon { get; set; }

        /// <summary>
        /// Does your attachment relate to something happening at a specific time?
        /// 
        /// By providing the ts field with an integer value in "epoch time", the attachment will display an additional
        /// timestamp value as part of the attachment's footer.
        /// 
        /// Use ts when referencing articles or happenings. Your message's timestamp will be displayed in varying ways,
        /// depending on how far in the past or future it is, relative to the present. Form factors, like mobile versus
        /// desktop may also transform its rendered appearance.
        /// </summary>
        [JsonConverter(typeof(DoubleUnixDateTimeConverter))]
        public DateTimeOffset ts { get; set; }
    }
}
