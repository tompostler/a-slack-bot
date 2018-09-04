using System.Collections.Generic;

namespace a_slack_bot.Documents
{
    public class Whitelist : BaseDocument
    {
        public override string Type => nameof(Whitelist);
        public override string Subtype { get; set; }

        public HashSet<string> values { get; set; }
    }
}
