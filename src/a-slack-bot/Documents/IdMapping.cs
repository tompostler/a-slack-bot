using System.Collections.Generic;

namespace a_slack_bot.Documents
{
    public class IdMapping : Base
    {
        public override string doctype => nameof(IdMapping);

        public Dictionary<string, string> mapping { get; set; }
    }
}
