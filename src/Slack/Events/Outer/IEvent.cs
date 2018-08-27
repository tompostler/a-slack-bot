using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Slack.Events.Outer
{
    /// <summary>
    /// The outer event for the Events API. This was manually made.
    /// </summary>
    /// <remarks>
    /// Documentation pulled from https://api.slack.com/types/event on 2018-08-06
    /// </remarks>
    [JsonConverter(typeof(IEventConverter))]
    public interface IEvent
    {
        /// <summary>
        /// Indicates which kind of event dispatch this is, usually <c>event_callback</c>
        /// </summary>
        /// <example>event_callback</example>
        string type { get; set; }

        /// <summary>
        /// A verification token to validate the event originated from Slack
        /// </summary>
        string token { get; set; }
    }

    public class IEventConverter : JsonConverter
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }

        private static object Lock = new object();
        private static Dictionary<string, Type> EventTypeMap;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // If the type map is null, populate it
            if (EventTypeMap == null)
                lock (Lock)
                    if (EventTypeMap == null)
                        EventTypeMap = typeof(IEventConverter).Assembly.GetTypes().Where(t => t.Namespace == typeof(IEventConverter).Namespace).ToDictionary(t => t.Name);

            // Get the event type and the default IEvent
            var jsonObject = JObject.Load(reader);
            var eventType = jsonObject[nameof(IEvent.type)].Value<string>();
            var @event = default(IEvent);

            // Create the proper event type
            if (EventTypeMap.ContainsKey(eventType))
                @event = (IEvent)Activator.CreateInstance(EventTypeMap[eventType]);
            else
                @event = Activator.CreateInstance<EventBase>();

            // Populate it
            serializer.Populate(jsonObject.CreateReader(), @event);
            return @event;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new InvalidOperationException("Use default serialization.");
        }
    }
}
