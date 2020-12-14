using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SlackEntry
    {
        [FunctionName(nameof(ReceiveSlashUptime))]
        public static async Task<HttpResponseMessage> ReceiveSlashUptime(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "receive/slash/uptime")]HttpRequestMessage req,
            ILogger logger)
        {
            if (Settings.Debug)
                // Trim off the beginning because of AI trying to "help"
                logger.LogInformation("Body: {0}", (await req.Content.ReadAsStringAsync()).Substring(10));

            // Make sure it's a legit request
            if (!await req.IsAuthed(logger))
                return req.CreateErrorResponse(HttpStatusCode.Unauthorized, "Did not match hash.");

            // Return the uptime
            return req.CreateResponse(HttpStatusCode.OK, new { response_type = "in_channel", text = $"```{C.UpTime:ddd\\.hh\\:mm\\:ss} ({C.InstanceId.Substring(0, Math.Min(C.InstanceId.Length, 8))})```" });
        }
    }
}
