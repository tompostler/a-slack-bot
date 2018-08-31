namespace a_slack_bot.Documents
{
    public class Response : BaseDocument
    {
        public override string Type => nameof(Response);
        public override string Subtype { get { return this.key; } set { } }

        public string key { get; set; }
        public string value { get; set; }
        public string user_id { get; set; }
    }
}
