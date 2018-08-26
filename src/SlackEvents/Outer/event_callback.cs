using Newtonsoft.Json;

namespace Slack.Events.Outer
{
    /// <summary>
    /// We package all event types delivered over the Events API in a common JSON-formatted event wrapper.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/types/event on 2018-08-26
    /// </remarks>
    public class event_callback : EventBase
    {
        /// <summary>
        /// The unique identifier of the workspace where the event occurred
        /// </summary>
        /// <example>T1H9RESGL</example>
        public string team_id { get; set; }

        /// <summary>
        /// The unique identifier your installed Slack application.
        /// Use this to distinguish which app the event belongs to if you use multiple apps with the same Request URL.
        /// </summary>
        /// <example>A2H9RFS1A</example>
        public string api_app_id { get; set; }

        /// <summary>
        /// The actual event, an object, that happened. You'll find the most variance in properties beneath this node.
        /// </summary>
        [JsonConverter(typeof(Inner.IEventConverter))]
        public Inner.IEvent @event { get; set; }

        /// <summary>
        /// A unique identifier for this specific event, globally unique across all workspaces.
        /// </summary>
        /// <example>Ev0PV52K25</example>
        public string event_id { get; set; }

        /// <summary>
        /// The epoch timestamp in seconds indicating when this event was dispatched.
        /// </summary>
        /// <example>1525215129</example>
        public int event_time { get; set; }

        /// <summary>
        /// An array of string-based User IDs. Each member of the collection represents a user that has installed your
        /// application/bot and indicates the described event would be visible to those users.
        /// </summary>
        public string[] authed_users { get; set; }
    }
}
