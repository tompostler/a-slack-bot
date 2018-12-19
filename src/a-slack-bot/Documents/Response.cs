using System;

namespace a_slack_bot.Documents
{
    public class Response : Base
    {
        public override string doctype => $"{nameof(Response)}|{this.key}";

        public string key { get; set; }
        public string value { get; set; }
        public string user_id { get; set; }

        public int count { get; set; }
        public Guid random { get; set; }
    }
}
