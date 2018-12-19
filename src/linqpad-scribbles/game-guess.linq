<Query Kind="Statements" />

Random random = new Random();
for (int balance = 1; balance <= 10_000; balance+=50)
{
	long winnings = 0;
	for (int i = 0; i < 1_000_000; i++)
	{
		var picked = random.Next(balance) + 1L;
		var guess = balance/2;
		var off = Math.Abs(picked - guess);
		var close = 100d / (off == 0 ? 1 : off);
		if (close < 1)
		{
			close = -1 / close;
		}
		winnings += (long)close;
	}
	Console.WriteLine($"{balance}: {winnings/1_000_000d}");
}