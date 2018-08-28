using Microsoft.Azure.Documents;

namespace a_slack_bot.Documents
{
    public abstract class IDocument<TContent> : Document
    {
        public abstract string DocumentType { get; }
        public abstract string DocumentClass { get; }
        public string Partition => DocumentType + '_' + DocumentClass;

        public abstract TContent Content { get; set; }
    }
}
