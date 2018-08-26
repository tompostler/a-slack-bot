using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Slack.Events.Inner.message_parts.items
{
    /// <summary>
    /// A shared base for items.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/events/reaction_added on 2018-08-26
    /// </remarks>
    public abstract class ItemBase
    {
        /// <summary>
        /// The type of item.
        /// </summary>
        public string type { get; set; }
    }

    public class ItemBaseConverter : JsonConverter
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }

        private static object Lock = new object();
        private static Dictionary<string, Type> ItemTypeMap;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // If the type map is null, populate it
            if (ItemTypeMap == null)
                lock (Lock)
                    if (ItemTypeMap == null)
                        ItemTypeMap = typeof(ItemBaseConverter).Assembly.GetTypes().Where(t => t.Namespace == typeof(ItemBaseConverter).Namespace).ToDictionary(t => t.Name);

            // Get the event type and the default IEvent
            var jsonObject = JObject.Load(reader);
            var itemType = jsonObject[nameof(ItemBase.type)].Value<string>();
            var item = default(ItemBase);

            // Create the proper event type
            if (ItemTypeMap.ContainsKey(itemType))
                item = (ItemBase)Activator.CreateInstance(ItemTypeMap[itemType]);
            else
                item = Activator.CreateInstance<ItemBase>();

            // Populate it
            serializer.Populate(jsonObject.CreateReader(), item);
            return item;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new InvalidOperationException("Use default serialization.");
        }
    }
}
