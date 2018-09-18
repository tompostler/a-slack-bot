using System.Net.Http;

namespace a_slack_bot.Functions
{
    public static partial class SBReceive
    {
        private static readonly HttpClient httpClient = new HttpClient();
    }
}
