using System.Collections.Generic;

namespace a_slack_bot.Documents
{
    public class IdMapping : BaseDocument<Dictionary<string, string>>
    {
        public override string Type => nameof(IdMapping);
        public override string Subtype { get; set; }
    }
}
