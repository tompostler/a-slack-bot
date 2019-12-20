using System;
using System.Text.RegularExpressions;

namespace a_slack_bot
{
    /// <summary>
    /// All of these methods are copied from prior projects. Can they be optimized and made better? Most likely, yes.
    /// </summary>
    internal static class RelativeDateTimeParsing
    {
        private static DateTime? ParseDateTimeToLocalWithoutThrowing(string toParse)
        {
            DateTime dt;
            try
            {
                if (toParse.Equals("now", StringComparison.OrdinalIgnoreCase))
                    dt = DateTime.Now;
                else if (!toParse.TryParseRelativeDateTime(out dt))
                    return null;
            }
            catch (Exception)
            {
                return null;
            }
            return dt.ToLocalTime();
        }

        public static string ToHumanReadble(string toParse)
        {
            var dtr = ParseDateTimeToLocalWithoutThrowing(toParse);
            if (!dtr.HasValue)
                return null;
            var dt = dtr.Value;

            // Now determine what to show the peoples based off of what was in the string _and_ how far back it actually was

            return DateTimeToHumanReadableBasedOnWhatToParse(toParse, dt);
        }

        private static string DateTimeToHumanReadableBasedOnWhatToParse(string toParse, DateTime dt)
        {
            toParse = toParse.ToLower();
            bool
                asksYear = toParse.Contains("year"),
                asksMont = toParse.Contains("month"),
                asksDays = toParse.Contains("day") || toParse.Contains("week") || toParse.Contains("yesterday") || toParse.Contains("today") || toParse.Contains("tomorrow"),
                asksHour = toParse.Contains("hour") || toParse.Contains("hr"),
                asksMinu = toParse.Contains("min"),
                asksSecs = toParse.Contains("sec"),
                asksNows = toParse.Contains("now");

            var now = DateTime.Now;
            bool
                shYea = asksYear || asksNows || dt.Year != now.Year,
                shMon = asksMont || asksNows || dt.Month != now.Month,
                shDay = asksDays || asksNows || dt.Day != now.Day,
                shTim = asksHour || asksMinu || asksNows || dt.Hour != now.Hour || dt.Minute != now.Minute,
                shSec = asksSecs || dt.Second != now.Second;

            // Nice human-readable date string example:
            //  Mon, Jun 7, 2017 at 1:45PM
            // Horribly broken up and mangled as conditional strings!
            string formatstr =
                // year, month, day         add day of week
                $"{(shYea && shMon && shDay ? "ddd, " : "")}" +
                // month                    add long month
                // month, year, day         add short month
                $"{(shMon || shDay ? (shYea && shDay ? "MMM " : "MMMM ") : "")}" +
                // day                      add day
                // day, year                add day and comma
                $"{(shDay ? (shYea ? "d, " : "d ") : "")}" +
                // year                     add year
                $"{(shYea ? "yyyy " : "")}" +
                // year, month, or day      add 'at'
                //  and time
                $"{((shYea || shMon || shDay) && shTim ? "'at' " : "")}" +
                // time                     add time
                $"{(shTim ? $"h:mm{(shSec ? ":ss" : "")}tt" : "")}";
            return dt.ToString(formatstr).Trim();
        }

        /// <summary>
        /// A class holding all the various regexes needed to match the things.
        /// While this can be extended and changed as necessary, assume English and slightly well-formatted strings.
        /// </summary>
        private static class R
        {
            private const RegexOptions opts = RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline;

            public const string Amt = @"(?:^last|^next|^current|^an?|(?:-\s?)?\d+)";
            public static readonly Regex Year = new Regex($@"{Amt}\s+year", opts);
            public static readonly Regex Month = new Regex($@"{Amt}\s+month", opts);
            public static readonly Regex Week = new Regex($@"{Amt}\s+week", opts);
            public static readonly Regex Day = new Regex($@"{Amt}\s+day", opts);
            public static readonly Regex SpecialDay = new Regex("(?:yesterday|today|tomorrow)", opts);
            public static readonly Regex Hour = new Regex($@"{Amt}\s+(?:hour|hr)", opts);
            public static readonly Regex Minute = new Regex($@"{Amt}\s+(?:minute|min)", opts);
            public static readonly Regex Second = new Regex($@"{Amt}\s+sec", opts);
            public static readonly Regex InPast = new Regex("(?:ago)", opts);
            // default to future computation

            private static readonly char[] SlashS = new char[] { ' ', '\t', '\r', '\n', '\f', '\v' };
            public static long FromMatch(string match)
            {
                int whitespaceIndex = match.IndexOfAny(SlashS);
                if (whitespaceIndex >= 0)
                    match = match.Substring(0, whitespaceIndex);
                if (match.StartsWith("0") && match.TrimStart('0').Length > 0)
                    match = match.TrimStart('0');

                if (long.TryParse(match, out long result))
                    return result;
                match = match.ToUpperInvariant();
                if (match.StartsWith("LAST"))
                    return -1;
                if (match.StartsWith("NEXT") || match.StartsWith("A"))
                    return 1;
                if (match.StartsWith("CURRENT"))
                    return 0;
                throw new ArgumentException("Didn't know what to do with " + nameof(match));
            }
            public static long FromMatch(Match match) => FromMatch(match.Value.Substring(0, match.Value.IndexOfAny(SlashS)));
        }

        /// <summary>
        /// Given a string, treat it as an engligh-formatted relative datetime and attempt to parse an actual datetime
        /// out. Parsed datetime is UTC.
        /// This method is to be treated as unstable until this warning is removed.
        /// </summary>
        private static bool TryParseRelativeDateTime(this string toParse, out DateTime result)
        {
            // Try for nicely formatted
            if (DateTime.TryParse(toParse, out result))
                return true;

            long yea = 0, mon = 0, wee = 0, day = 0, spe = 0, hou = 0, min = 0, sec = 0;

            bool success = false;
            var match = R.Year.Match(toParse);
            if (match.Success)
            {
                yea = R.FromMatch(match);
                success = true;
            }
            match = R.Month.Match(toParse);
            if (match.Success)
            {
                mon = R.FromMatch(match);
                success = true;
            }
            match = R.Week.Match(toParse);
            if (match.Success)
            {
                wee = R.FromMatch(match);
                success = true;
            }
            match = R.Day.Match(toParse);
            if (match.Success)
            {
                day = R.FromMatch(match);
                success = true;
            }
            match = R.SpecialDay.Match(toParse);
            if (match.Success)
            {
                switch (match.Value.ToUpperInvariant())
                {
                    case "YESTERDAY":
                        spe = -1;
                        break;
                    case "TODAY":
                        spe = 0;
                        break;
                    case "TOMORROW":
                        spe = 1;
                        break;
                }
                success = true;
            }
            match = R.Hour.Match(toParse);
            if (match.Success)
            {
                hou = R.FromMatch(match);
                success = true;
            }
            match = R.Minute.Match(toParse);
            if (match.Success)
            {
                min = R.FromMatch(match);
                success = true;
            }
            match = R.Second.Match(toParse);
            if (match.Success)
            {
                sec = R.FromMatch(match);
                success = true;
            }

            result = default;
            if (!success)
                return false;

            try
            {
                if (yea > int.MaxValue || mon > int.MaxValue)
                    return false;

                result = R.InPast.IsMatch(toParse)
                    ? DateTime.UtcNow
                        .AddYears(-(int)yea)
                        .AddMonths(-(int)mon)
                        .AddDays(-wee * 7 - day - spe)
                        .AddHours(-hou)
                        .AddMinutes(-min)
                        .AddSeconds(-sec)
                    : DateTime.UtcNow
                        .AddYears((int)yea)
                        .AddMonths((int)mon)
                        .AddDays(wee * 7 + day + spe)
                        .AddHours(hou)
                        .AddMinutes(min)
                        .AddSeconds(sec);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            return true;
        }
    }
}
