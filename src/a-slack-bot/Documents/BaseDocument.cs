using Microsoft.Azure.Documents;

namespace a_slack_bot.Documents
{
    public abstract class BaseDocument : Resource
    {
        public abstract string Type { get; }
        public abstract string Subtype { get; set; }

        // CDB Partition Key
        public string TypeSubtype => this.Type + '|' + this.Subtype;
    }

    public abstract class BaseDocument<TContent> : BaseDocument
    {
        public TContent Content { get; set; }
    }
}
