using System.Reflection;

namespace Slack.Events.Outer
{
    /// <summary>
    /// The outer event for the Events API. This was manually made.
    /// </summary>
    /// <remarks>
    /// Documentation pulled from https://api.slack.com/types/event on 2018-08-06
    /// </remarks>
    public abstract class EventBase : IEvent
    {
        /// <summary>
        /// Indicates which kind of event dispatch this is, usually <c>event_callback</c>
        /// </summary>
        /// <example>event_callback</example>
        public string type { get; set; }

        /// <summary>
        /// A verification token to validate the event originated from Slack
        /// </summary>
        public string token { get; set; }
    }
}
