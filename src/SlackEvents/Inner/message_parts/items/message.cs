using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace Slack.Events.Inner.message_parts.items
{
    /// <summary>
    /// See <see cref="Inner.message"/>
    /// </summary>
    public class message : ItemBase
    {
        public string channel { get; set; }
        [JsonConverter(typeof(DoubleUnixDateTimeConverter))]
        public DateTimeOffset ts { get; set; }
    }
}
