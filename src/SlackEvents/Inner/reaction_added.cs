using Newtonsoft.Json;

namespace Slack.Events.Inner
{
    /// <summary>
    /// A member has added an emoji reaction to an item
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/events/reaction_added on 2018-08-26
    /// </remarks>
    public class reaction_added : EventBase
    {
        /// <summary>
        /// This field indicates the ID of the user who performed this event.
        /// </summary>
        public string user { get; set; }

        /// <summary>
        /// The emoji reaction.
        /// </summary>
        public string reaction { get; set; }

        /// <summary>
        /// This field represents the ID of the user that created the original item that has been reacted to.
        /// </summary>
        public string item_user { get; set; }

        /// <summary>
        /// This field is a brief reference to what was reacted to.
        /// </summary>
        [JsonConverter(typeof(message_parts.items.ItemBaseConverter))]
        public message_parts.items.ItemBase item { get; set; }
    }
}
