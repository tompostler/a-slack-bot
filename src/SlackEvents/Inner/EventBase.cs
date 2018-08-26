using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace Slack.Events.Inner
{
    /// <summary>
    /// The actual event, an object, that happened. You'll find the most variance in properties beneath this node.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/types/event on 2018-08-26
    /// </remarks>
    public class EventBase : IEvent
    {
        /// <summary>
        /// The specific name of the event
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// When the event was dispatched
        /// </summary>
        [JsonConverter(typeof(DoubleUnixDateTimeConverter))]
        public DateTimeOffset event_ts { get; set; }
    }
}
