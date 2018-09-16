using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
        public override string Type => "Game";
        public override string Subtype { get => nameof(Blackjack); set { } }

        public string user_start { get; set; }
        [JsonIgnore]
        public string thread_ts { get { return this.Id; } set { this.Id = value; } }

        public Dictionary<string, List<string>> hands { get; set; } = new Dictionary<string, List<string>>();
        public List<BlackjackMove> moves { get; set; } = new List<BlackjackMove>();
    }

    public class BlackjackMove
    {
        public string user_id { get; set; }
        public BlackjackAction action { get; set; }

        public string bet { get; set; }
        public string card { get; set; }
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
}
