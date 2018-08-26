using System;

namespace a_slack_bot
{
    public static class Extensions
    {
        public static string ToHexString(this byte[] @this)
        {
            return BitConverter.ToString(@this).Replace("-", "").ToLower();
        }
    }
}
