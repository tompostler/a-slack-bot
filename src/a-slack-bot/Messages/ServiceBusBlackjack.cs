﻿namespace a_slack_bot.Messages
{
    public sealed class ServiceBusBlackjack
    {
        public BlackjackMessageType type { get; set; }
        public string channel_id { get; set; }
        public string thread_ts { get; set; }
        public string user_id { get; set; }
        public long amount { get; set; }
        public Documents2.BlackjackActionType action { get; set; }
    }

    public enum BlackjackMessageType
    {
        GetBalance,
        GetBalances,
        UpdateBalance,

        Timer_Joining,
        Timer_CollectingBets,
        Timer_Running,

        JoinGame,
        ToCollectingBets,
        PlaceBet,
        ToGame,
        GameAction,
        DealerPlay,
        ToFinish
    }
}
