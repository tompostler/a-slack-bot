using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

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
        [JsonConverter(typeof(DoubleUnixDateTimeConverter))]
        public DateTimeOffset ts { get; set; }
    }
}
