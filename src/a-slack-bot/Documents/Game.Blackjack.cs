﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace a_slack_bot.Documents
{
    public class Blackjack : Base
    {
        public override string doctype => nameof(Blackjack);

        public override string Id { get => this.thread_ts; set { } }

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
                        ts /= Max3DigitBase33;
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

        public List<string> users { get; set; } = new List<string>();
        // key=user_id,value=cards. quick state that can be reconstructed from actions
        public Dictionary<string, List<Cards.Cards>> hands { get; set; } = new Dictionary<string, List<Cards.Cards>>();
        // key=user_id,value=bet. quick state that can be reconstructed from actions
        public Dictionary<string, long> bets { get; set; } = new Dictionary<string, long>();
        public List<BlackjackAction> actions { get; set; } = new List<BlackjackAction>();

        public BlackjackGameState state { get; set; } = BlackjackGameState.Joining;
        public int user_active { get; set; }
    }

    public class BlackjackAction
    {
        public string user_id { get; set; }
        public BlackjackActionType type { get; set; }

        public long? amount { get; set; }
        public Cards.Cards? card { get; set; }
        public BlackjackGameState? to_state { get; set; }

        public DateTimeOffset timestamp { get; set; } = DateTimeOffset.Now;
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BlackjackActionType
    {
        Invalid,
        StateChange,
        Join,
        Bet,
        BalanceChange,
        Deal,
        Prompt,
        Hit,
        Stand,
        Double,
        Split,
        Surrender
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
