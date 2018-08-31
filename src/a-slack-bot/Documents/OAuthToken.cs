namespace a_slack_bot.Documents
{
    public class OAuthToken : BaseDocument
    {
        public override string Type => nameof(OAuthToken);
        public override string Subtype { get; set; }

        public string token { get; set; }
    }
}
