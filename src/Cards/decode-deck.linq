<Query Kind="Statements">
  <Reference Relative="bin\Debug\net47\Cards.dll"></Reference>
  <Reference Relative="bin\Debug\net47\Newtonsoft.Json.dll"></Reference>
  <Namespace>Cards</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
</Query>

Deck deck = new DeckStore
{
	Index = int.TryParse(Console.ReadLine().Trim(), out int idx) ? idx : 0,
	NumDecks = int.TryParse(Console.ReadLine().Trim(), out int numd) ? numd : 1,
	Seed = int.TryParse(Console.ReadLine().Trim(), out int seed) ? seed : 1
};
Console.WriteLine($"Deck: {JsonConvert.SerializeObject((DeckStore)deck)}");

var numCards = int.TryParse(Console.ReadLine().Trim(), out int numc) ? numc : 10;
Console.WriteLine($"First {numCards} cards:");
while (numCards-- > 0)
	Console.Write(deck.Deal().ToString() + ' ');