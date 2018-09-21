using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace a_slack_bot.Documents
{
    public class BlackjackStandings : BaseDocument<Dictionary<string, long>>
    {
        [JsonIgnore]
        public static readonly Uri DocUri = Microsoft.Azure.Documents.Client.UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, nameof(Documents.BlackjackStandings));

        public override string Type => "Game";
        public override string Subtype { get => nameof(Blackjack); set { } }
        public override string Id { get => nameof(BlackjackStandings); set { } }
    }

    public class Blackjack : BaseDocument
    {
        [JsonIgnore]
        public static readonly Uri DocColUri = Microsoft.Azure.Documents.Client.UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN);
        [JsonIgnore]
        public static readonly Microsoft.Azure.Documents.PartitionKey PartitionKey = new Microsoft.Azure.Documents.PartitionKey("Game|" + nameof(Blackjack));

        public override string Id { get => $"{channel_id}|{thread_ts}"; set { var parts = value.Split('|'); channel_id = parts[0]; thread_ts = parts[1]; } }
        public override string Type => "Game";
        public override string Subtype { get => nameof(Blackjack); set { } }

        public string user_start { get; set; }
        public string channel_id { get; set; }
        public string thread_ts { get; set; }

        private string _friendly_name { get; set; }
        public string friendly_name
        {
            get
            {
                if (this._friendly_name == null)
                {
                    long ts = long.Parse(this.thread_ts.Replace(".", string.Empty));
                    var friendly = string.Empty;
                    do
                    {
                        const long Max3DigitBase33 = 35937;
                        var chunk = ts % Max3DigitBase33;
                        friendly = '.' + chunk.ToBase33String().PadLeft(3, '0') + friendly;
                        ts = ts / Max3DigitBase33;
                    }
                    while (ts > 0);
                    this._friendly_name = friendly.TrimStart('.', '0');
                }
                return this._friendly_name;
            }
            set
            {
                this._friendly_name = value;
            }
        }

        public Cards.DeckStore deck { get; set; }

        // key=user_id,value=cards. quick state that can be reconstructed from moves
        public Dictionary<string, List<Cards.Cards>> hands { get; set; } = new Dictionary<string, List<Cards.Cards>>();
        // key=user_id,value=bet. quick state that can be reconstructed from moves
        public Dictionary<string, long> bets { get; set; } = new Dictionary<string, long>();
        public List<BlackjackMove> moves { get; set; } = new List<BlackjackMove>();

        public BlackjackGameState state { get; set; } = BlackjackGameState.Joining;
        public string state_user { get; set; }
    }

    public class BlackjackMove
    {
        public string user_id { get; set; }
        public BlackjackAction action { get; set; }

        public long bet { get; set; }
        public Cards.Cards? card { get; set; }
        public BlackjackGameState? to_state { get; set; }

        public DateTimeOffset timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BlackjackAction
    {
        Invalid,
        StateChange,
        Join,
        Bet,
        Fold,
        Hit,
        Split
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BlackjackGameState
    {
        None,
        Joining,
        CollectingBets,
        Running,
        Finished
    }
}
