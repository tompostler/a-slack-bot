namespace a_slack_bot.Documents
{
    public class OAuthToken : BaseDocument<string>
    {
        public override string Type => nameof(OAuthToken);
        public override string Subtype => this.token_type;

        public string token_type { get; set; }
    }
}
