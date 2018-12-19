namespace a_slack_bot.Documents
{
    public class OAuthUserToken : Base
    {
        public override string doctype => nameof(OAuthUserToken);

        public string token { get; set; }
    }
}
