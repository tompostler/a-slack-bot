using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SlackEntry
    {
        [FunctionName(nameof(ReceiveSlashBlackjack))]
        public static async Task<HttpResponseMessage> ReceiveSlashBlackjack(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "receive/slash/blackjack")]HttpRequestMessage req,
            ILogger logger)
        {
            if (Settings.Debug)
                // Trim off the beginning because of AI trying to "help"
                logger.LogInformation("Body: {0}", (await req.Content.ReadAsStringAsync()).Substring(10));

            // Make sure it's a legit request
            if (!await req.IsAuthed(logger))
                return req.CreateErrorResponse(HttpStatusCode.Unauthorized, "Did not match hash.");

            // Return the version
            return req.CreateResponse(HttpStatusCode.OK, new { response_type = "ephemeral", text = "*NOT YET IMPLEMENTED*" });
        }
    }
}
