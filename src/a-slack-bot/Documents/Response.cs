using Newtonsoft.Json;
using System.Collections.Generic;

namespace a_slack_bot.Documents
{
    public class Response : BaseDocument<string>
    {
        public override string Type => nameof(Response);
        public override string Subtype { get; set; }

        [JsonIgnore]
        public string key { get => this.Subtype; set => this.Subtype = value; }
        [JsonIgnore]
        public string value { get => this.Content; set => this.Content = value; }

        public string user_id { get; set; }
    }

    public class ResponsesUsed : BaseDocument<HashSet<string>>
    {
        public override string Id { get => nameof(ResponsesUsed); set { } }
        public override string Type => nameof(Response);
        public override string Subtype { get; set; }

        [JsonIgnore]
        public string key { get => this.Subtype; set => this.Subtype = value; }
        [JsonIgnore]
        public HashSet<string> ids_used { get => this.Content; set => this.Content = value; }
    }
}
