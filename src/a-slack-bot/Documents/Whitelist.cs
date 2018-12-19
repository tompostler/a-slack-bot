using System.Collections.Generic;

namespace a_slack_bot.Documents
{
    public class Whitelist : Base
    {
        public override string doctype => nameof(Whitelist);

        /// <summary>
        /// Whitelist. Key = whitelist thing, Value = slack concept whitelisted on
        /// </summary>
        public Dictionary<string, HashSet<string>> values { get; set; }
    }
}
