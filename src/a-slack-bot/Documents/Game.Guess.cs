namespace a_slack_bot.Documents
{
    public class Guess : Base
    {
        public override string doctype => nameof(Guess);

        public override string Id { get => this.trigger_id; set { } }

        public string user_id { get; set; }
        public string channel_id { get; set; }
        public string trigger_id { get; set; }

        public long balance_start { get; set; }
        public long guess { get; set; }
        public long actual { get; set; }
    }
}
