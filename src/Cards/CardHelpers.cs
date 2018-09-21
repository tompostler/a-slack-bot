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
            switch (card)
            {
                case Cards.C2: return "🃒";
                case Cards.C3: return "🃓";
                case Cards.C4: return "🃔";
                case Cards.C5: return "🃕";
                case Cards.C6: return "🃖";
                case Cards.C7: return "🃗";
                case Cards.C8: return "🃘";
                case Cards.C9: return "🃙";
                case Cards.CT: return "🃚";
                case Cards.CJ: return "🃛";
                case Cards.CQ: return "🃝";
                case Cards.CK: return "🃞";
                case Cards.CA: return "🃑";
                case Cards.D2: return "🃂";
                case Cards.D3: return "🃃";
                case Cards.D4: return "🃄";
                case Cards.D5: return "🃅";
                case Cards.D6: return "🃆";
                case Cards.D7: return "🃇";
                case Cards.D8: return "🃈";
                case Cards.D9: return "🃉";
                case Cards.DT: return "🃊";
                case Cards.DJ: return "🃋";
                case Cards.DQ: return "🃍";
                case Cards.DK: return "🃎";
                case Cards.DA: return "🃁";
                case Cards.H2: return "🂲";
                case Cards.H3: return "🂳";
                case Cards.H4: return "🂴";
                case Cards.H5: return "🂵";
                case Cards.H6: return "🂶";
                case Cards.H7: return "🂷";
                case Cards.H8: return "🂸";
                case Cards.H9: return "🂹";
                case Cards.HT: return "🂺";
                case Cards.HJ: return "🂻";
                case Cards.HQ: return "🂽";
                case Cards.HK: return "🂾";
                case Cards.HA: return "🂱";
                case Cards.S2: return "🂢";
                case Cards.S3: return "🂣";
                case Cards.S4: return "🂤";
                case Cards.S5: return "🂥";
                case Cards.S6: return "🂦";
                case Cards.S7: return "🂧";
                case Cards.S8: return "🂨";
                case Cards.S9: return "🂩";
                case Cards.ST: return "🂪";
                case Cards.SJ: return "🂫";
                case Cards.SQ: return "🂭";
                case Cards.SK: return "🂮";
                case Cards.SA: return "🂡";
                default: return "🂠";
            }
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
