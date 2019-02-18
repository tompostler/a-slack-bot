using System;

namespace a_slack_bot.Documents
{
    public abstract class ReThings : Base
    {
        public override abstract string doctype { get; }

        public string key { get; set; }
        public string value { get; set; }
        public string user_id { get; set; }

        public int count { get; set; }
        public Guid random { get; set; }
    }

    public class Reaction : ReThings
    {
        public override string doctype => $"{nameof(Reaction)}|{this.key}";
    }

    public class Response : ReThings
    {
        public override string doctype => $"{nameof(Response)}|{this.key}";
    }
}
