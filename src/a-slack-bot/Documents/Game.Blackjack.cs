using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace a_slack_bot.Documents
{
    public class BlackjackStandings : BaseDocument<Dictionary<string, ulong>>
    {
        public override string Type => "Game";
        public override string Subtype { get => nameof(Blackjack); set { } }
        public override string Id { get => nameof(BlackjackStandings); set { } }
    }

    public class Blackjack : BaseDocument
    {
        public override string Id { get => $"{channel_id}|{thread_ts}"; set { var parts = value.Split('|'); channel_id = parts[0]; thread_ts = parts[1]; } }
        public override string Type => "Game";
        public override string Subtype { get => nameof(Blackjack); set { } }

        public string user_start { get; set; }
        public string channel_id { get; set; }
        public string thread_ts { get; set; }

        // key=user_id,value=cards. quick state that can be reconstructed from moves
        public Dictionary<string, List<string>> hands { get; set; } = new Dictionary<string, List<string>>();
        public List<BlackjackMove> moves { get; set; } = new List<BlackjackMove>();
        public BlackjackGameState state { get; set; } = BlackjackGameState.Pending;
    }

    public class BlackjackMove
    {
        public string user_id { get; set; }
        public BlackjackAction action { get; set; }

        public string bet { get; set; }
        public string card { get; set; }

        public DateTimeOffset timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BlackjackAction
    {
        Invalid,
        Join,
        Bet,
        Fold,
        Hit,
        Split
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BlackjackGameState
    {
        Pending,
        Running,
        Finished
    }
}
