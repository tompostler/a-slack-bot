using a_slack_bot.Messages;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace a_slack_bot
{
    public static class Functions
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly Random Random = new Random();

        [FunctionName(nameof(ReceiveEvent))]
        public static async Task<HttpResponseMessage> ReceiveEvent(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "receive")]HttpRequestMessage req,
            [ServiceBus(Constants.SBQ.Input, AccessRights.Manage)]IAsyncCollector<ServiceBusInput> messageCollector,
            ILogger logger)
        {
            // Grab the body
            var body = await req.Content.ReadAsStringAsync();
            if (Settings.Debug)
                logger.LogInformation("Body: {0}", body);

            // Make sure it's a legit request
            var hasher = new HMACSHA256(Settings.SlackSigningSecretBytes);
            var hashComputed = "v0=" + hasher.ComputeHash(Encoding.UTF8.GetBytes($"v0:{req.Headers.GetValues(Constants.Headers.Slack.RequestTimestamp).First()}:{body}")).ToHexString();
            var hashExpected = req.Headers.GetValues(Constants.Headers.Slack.Signature).First();
            logger.LogInformation("Sig check. Computed:{0} Expected:{1}", hashComputed, hashExpected);
            if (hashComputed != hashExpected)
                return req.CreateErrorResponse(HttpStatusCode.Unauthorized, "Did not match hash.");

            // Get stuff from the message
            var outerEvent = await req.Content.ReadAsAsync<Slack.Events.Outer.IEvent>();

            // Check if it's a first-time challenge since we can't process this one async
            if (outerEvent is Slack.Events.Outer.url_verification)
            {
                logger.LogInformation("First time challenge. Returning.");
                return req.CreateResponse(HttpStatusCode.OK, outerEvent);
            }

            // Send it off to be processed
            if (outerEvent is Slack.Events.Outer.event_callback)
            {
                logger.LogInformation("Sending inner event into the queue.");
                await messageCollector.AddAsync(new ServiceBusInput
                {
                    @event = ((Slack.Events.Outer.event_callback)outerEvent).@event
                });
            }

            // Return all is well
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [FunctionName(nameof(Ping))]
        public static HttpResponseMessage Ping([HttpTrigger(AuthorizationLevel.Function, "get", Route = "ping")]HttpRequestMessage req)
        {
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [FunctionName(nameof(Debug))]
        public static async Task<HttpResponseMessage> Debug([HttpTrigger(AuthorizationLevel.Function, "post", Route = "debug")]HttpRequestMessage req)
        {
            var thing = await req.Content.ReadAsAsync<Slack.Events.Outer.IEvent>();

            return req.CreateResponse(HttpStatusCode.OK, thing);
        }
    }
}
