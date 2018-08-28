namespace a_slack_bot.Documents
{
    interface IDocument<TContent> : IDocument
    {
        TContent Content { get; set; }
    }

    interface IDocument
    {
        string DocType { get; }
        string DocSubtype { get; }
    }
}
