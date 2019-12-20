using System;
using System.Collections.Generic;

namespace Cards
{
    /// <summary>
    /// Helper methods for the card enums
    /// </summary>
    public static class CardHelpers
    {
        private static string GetUnicodeRepresentation(this Cards card)
        {
            return card switch
            {
                Cards.C2 => "🃒",
                Cards.C3 => "🃓",
                Cards.C4 => "🃔",
                Cards.C5 => "🃕",
                Cards.C6 => "🃖",
                Cards.C7 => "🃗",
                Cards.C8 => "🃘",
                Cards.C9 => "🃙",
                Cards.CT => "🃚",
                Cards.CJ => "🃛",
                Cards.CQ => "🃝",
                Cards.CK => "🃞",
                Cards.CA => "🃑",
                Cards.D2 => "🃂",
                Cards.D3 => "🃃",
                Cards.D4 => "🃄",
                Cards.D5 => "🃅",
                Cards.D6 => "🃆",
                Cards.D7 => "🃇",
                Cards.D8 => "🃈",
                Cards.D9 => "🃉",
                Cards.DT => "🃊",
                Cards.DJ => "🃋",
                Cards.DQ => "🃍",
                Cards.DK => "🃎",
                Cards.DA => "🃁",
                Cards.H2 => "🂲",
                Cards.H3 => "🂳",
                Cards.H4 => "🂴",
                Cards.H5 => "🂵",
                Cards.H6 => "🂶",
                Cards.H7 => "🂷",
                Cards.H8 => "🂸",
                Cards.H9 => "🂹",
                Cards.HT => "🂺",
                Cards.HJ => "🂻",
                Cards.HQ => "🂽",
                Cards.HK => "🂾",
                Cards.HA => "🂱",
                Cards.S2 => "🂢",
                Cards.S3 => "🂣",
                Cards.S4 => "🂤",
                Cards.S5 => "🂥",
                Cards.S6 => "🂦",
                Cards.S7 => "🂧",
                Cards.S8 => "🂨",
                Cards.S9 => "🂩",
                Cards.ST => "🂪",
                Cards.SJ => "🂫",
                Cards.SQ => "🂭",
                Cards.SK => "🂮",
                Cards.SA => "🂡",
                _ => "🂠",
            };
        }

        /// <summary>
        /// Convert a <see cref="Cards"/> value to a nice string. E.g. 'Ace of Spades'.
        /// <see cref="Cards.Invalid"/> represents a 'Hidden' card.
        /// </summary>
        public static string ToNiceString(this Cards card)
        {
            if (card != Cards.Invalid)
                return $"{card.GetUnicodeRepresentation()} {Enum.GetName(typeof(CardNumber), card.ToNumber())} of {Enum.GetName(typeof(CardSuit), card.ToSuit())}";
            else
                return $"{card.GetUnicodeRepresentation()} Hidden";
        }

        /// <summary>
        /// Convert a <see cref="Cards"/> value to a <see cref="CardNumber"/>.
        /// </summary>
        public static CardNumber ToNumber(this Cards card)
        {
            return (CardNumber)((int)card & 0xF);
        }

        /// <summary>
        /// Convert a <see cref="Cards"/> value to a <see cref="CardSuit"/>.
        /// </summary>
        public static CardSuit ToSuit(this Cards card)
        {
            return (CardSuit)((int)card & 0xF0);
        }

        /// <summary>
        /// Helper class for <see cref="GetBlackjackScore(List{Cards})"/>.
        /// </summary>
        public class BlackjackScore
        {
            /// <summary>
            /// The number of cards in the scored hand.
            /// </summary>
            public int CardCount { get; set; }
            /// <summary>
            /// The value of the scored hand, with as many soft Aces as possible.
            /// </summary>
            public int Value { get; set; }
            /// <summary>
            /// An indication if the Aces were treated softly (as 11s).
            /// </summary>
            public bool IsSoft { get; set; }

            public bool IsBlackjack => this.CardCount == 2 && this.Value == 21;
            public bool IsBust => this.Value > 21;
        }

        /// <summary>
        /// Given a hand, compute the blackjack score.
        /// </summary>
        public static BlackjackScore GetBlackjackScore(this List<Cards> cards)
        {
            var score = new BlackjackScore { CardCount = cards.Count };

            // First round, count Aces as +1
            foreach (var card in cards)
                score.Value += card.ToBlackjackWorthWhenAceIsOne();

            // Optimization, since busting is common
            if (score.IsBust)
                return score;

            // Second round, count Aces as +10 if <21
            foreach (var card in cards)
                if (card.ToNumber() == CardNumber.Ace && score.Value + 10 <= 21)
                {
                    score.Value += 10;
                    score.IsSoft = true;
                }

            return score;
        }

        private static int ToBlackjackWorthWhenAceIsOne(this Cards card)
        {
            switch (card.ToNumber())
            {
                case CardNumber.Jack:
                case CardNumber.Queen:
                case CardNumber.King:
                    return 10;
                case CardNumber.Ace:
                    return 1;
                default:
                    return (int)card.ToNumber();
            }
        }
    }
}
