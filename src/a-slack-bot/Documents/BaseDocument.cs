using Microsoft.Azure.Documents;

namespace a_slack_bot.Documents
{
    public abstract class BaseDocument : Document
    {
        public string DocType { get; set; }
        public string DocSubtype { get; set; }
    }
}
