using System.Collections.Generic;

namespace a_slack_bot.Documents
{
    public class Response : BaseDocument<string>
    {
        public override string Type => nameof(Response);
        public override string Subtype { get; set; }

        public string user_id { get; set; }
    }

    public class ResponsesUsed : BaseDocument<HashSet<string>>
    {
        public override string Id { get => nameof(ResponsesUsed); set { } }
        public override string Type => nameof(Response);
        public override string Subtype { get; set; }
    }
}
