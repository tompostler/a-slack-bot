﻿using Newtonsoft.Json;

namespace a_slack_bot.Messages
{
    public sealed class InputEvent
    {
        [JsonConverter(typeof(Slack.Events.Inner.IEventConverter))]
        public Slack.Events.Inner.IEvent @event { get; set; }
    }
}
