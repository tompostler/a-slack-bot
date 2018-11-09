using System;

namespace Cards
{
    /// <summary>
    /// Work with a deck of cards.
    /// </summary>
    /// <remarks>
    /// Yes, it's using the "bad" <see cref="Random"/> class.
    /// Yes, more than <see cref="Int32.MaxValue"/> cards will cause massive problems.
    /// Yes, it's not the most efficient thing out there.
    /// </remarks>
    public sealed class Deck
    {
        private static readonly Cards[] OneDeck = new Cards[]
        {
            Cards.C2, Cards.C3, Cards.C4, Cards.C5, Cards.C6, Cards.C7, Cards.C8,
            Cards.C9, Cards.CT, Cards.CJ, Cards.CQ, Cards.CK, Cards.CA,

            Cards.D2, Cards.D3, Cards.D4, Cards.D5, Cards.D6, Cards.D7, Cards.D8,
            Cards.D9, Cards.DT, Cards.DJ, Cards.DQ, Cards.DK, Cards.DA,

            Cards.H2, Cards.H3, Cards.H4, Cards.H5, Cards.H6, Cards.H7, Cards.H8,
            Cards.H9, Cards.HT, Cards.HJ, Cards.HQ, Cards.HK, Cards.HA,

            Cards.S2, Cards.S3, Cards.S4, Cards.S5, Cards.S6, Cards.S7, Cards.S8,
            Cards.S9, Cards.ST, Cards.SJ, Cards.SQ, Cards.SK, Cards.SA
        };

        internal Cards[] deck;
        internal int idx;
        internal int numDecks;
        internal int seed;
        internal Random random;

        /// <summary>
        /// Ctor. Shuffles the deck.
        /// </summary>
        public Deck()
            : this(1)
        { }

        /// <summary>
        /// Ctor for more decks. Shuffles the deck.
        /// </summary>
        /// <remarks>
        /// <see cref="Random"/> normally uses <see cref="Environment.TickCount"/>, but we may be close on repeated
        /// startups by Azure Functions. So use the <see cref="DateTime.Ticks"/> from <see cref="DateTime.Now"/>
        /// rounded to the nearest 100 microseconds and then truncated to an int instead to give us a rolling 48-day
        /// window of 100-microsecond slots for repeats to occur.
        /// </remarks>
        public Deck(int numDecks)
            : this(numDecks, (int)(DateTime.UtcNow.Ticks / 1_000), 0)
        { }

        internal Deck(int numDecks, int seed, int idx)
        {
            this.numDecks = numDecks;
            this.seed = seed;
            this.idx = idx;
            this.random = new Random(seed);
            this.deck = new Cards[52 * numDecks];
            for (int i = 0; i < this.deck.Length; i++)
                this.deck[i] = Deck.OneDeck[i % 52];
            for (int i = 0; i <= this.idx / this.deck.Length; i++)
                this.Shuffle();
        }

        /// <summary>
        /// Shuffles the deck. Sets the index to 0.
        /// </summary>
        public void Shuffle()
        {
            int n = this.deck.Length;
            while (n > 1)
            {
                int k = this.random.Next(n--);
                var temp = this.deck[n];
                this.deck[n] = this.deck[k];
                this.deck[k] = temp;
            }
        }

        /// <summary>
        /// Deals the next card. If the end of the deck was reached, shuffles before dealing.
        /// </summary>
        public Cards Deal()
        {
            if (this.idx > this.deck.Length)
                this.Shuffle();
            return this.deck[this.idx++ % this.deck.Length];
        }
    }

    /// <summary>
    /// Use this type to store a deck. Saves the minimal information required to recreate a deck.
    /// </summary>
    public sealed class DeckStore
    {
        public int Index { get; set; }
        public int NumDecks { get; set; }
        public int Seed { get; set; }

        public static implicit operator Deck(DeckStore store)
        {
            return new Deck(store.NumDecks, store.Seed, store.Index);
        }

        public static implicit operator DeckStore(Deck deck)
        {
            return new DeckStore
            {
                Index = deck.idx,
                NumDecks = deck.numDecks,
                Seed = deck.seed
            };
        }
    }
}
