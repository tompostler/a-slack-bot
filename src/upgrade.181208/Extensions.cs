namespace a_slack_bot
{
    public static class Extensions
    {
        public static string ToBase33String(this long @this)
        {
            const string base33Alphabet = "0123456789ABCDEFGHJKLMNPQRSTUVXYZ";
            string basic = string.Empty;

            do
            {
                long remainder = @this % base33Alphabet.Length;
                basic = base33Alphabet[(int)remainder] + basic;
                @this = (@this - remainder) / base33Alphabet.Length;
            }
            while (@this > 0);

            return basic;
        }
    }
}
