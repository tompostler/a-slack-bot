using a_slack_bot.Messages;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static class SlackEntry
    {
        [FunctionName(nameof(ReceiveEvent))]
        public static async Task<HttpResponseMessage> ReceiveEvent(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "receive/event")]HttpRequestMessage req,
            [ServiceBus(Constants.SBQ.InputEvent, AccessRights.Manage)]IAsyncCollector<ServiceBusInputEvent> messageCollector,
            ILogger logger)
        {
            if (Settings.Debug)
                logger.LogInformation("Body: {0}", await req.Content.ReadAsStringAsync());

            // Make sure it's a legit request
            if (!await req.IsAuthed(logger))
                return req.CreateErrorResponse(HttpStatusCode.Unauthorized, "Did not match hash.");

            // Get stuff from the message
            var outerEvent = await req.Content.ReadAsAsync<Slack.Events.Outer.IEvent>();

            // Check if it's a first-time challenge since we can't process those async
            if (outerEvent is Slack.Events.Outer.url_verification)
            {
                logger.LogInformation("First time challenge. Returning it.");
                return req.CreateResponse(HttpStatusCode.OK, outerEvent);
            }

            // Send it off to be processed
            if (outerEvent is Slack.Events.Outer.event_callback)
            {
                logger.LogInformation("Sending inner event into the queue.");
                await messageCollector.AddAsync(new ServiceBusInputEvent
                {
                    @event = ((Slack.Events.Outer.event_callback)outerEvent).@event
                });
            }

            // Return all is well
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [FunctionName(nameof(ReceiveSlash))]
        public static async Task<HttpResponseMessage> ReceiveSlash(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "receive/slash")]HttpRequestMessage req,
            [ServiceBus(Constants.SBQ.InputSlash, AccessRights.Manage)]IAsyncCollector<ServiceBusInputSlash> messageCollector,
            ILogger logger)
        {
            if (Settings.Debug)
                // Trim off the beginning because of AI trying to "help"
                logger.LogInformation("Body: {0}", (await req.Content.ReadAsStringAsync()).Substring(10));

            // Make sure it's a legit request
            if (!await req.IsAuthed(logger))
                return req.CreateErrorResponse(HttpStatusCode.Unauthorized, "Did not match hash.");

            // Get stuff from the message
            var slashData = await req.Content.ReadAsFormDataAsync<Slack.Slash>();

            // Send it off to be processed
            logger.LogInformation("Sending slash command into the queue.");
            await messageCollector.AddAsync(new ServiceBusInputSlash
            {
                slashData = slashData
            });

            // Return all is well
            return req.CreateResponse(HttpStatusCode.OK, new { response_type = "ephemeral", text = $"{slashData.command} {slashData.text}" });
        }

        [FunctionName(nameof(ReceiveSlashVersion))]
        public static async Task<HttpResponseMessage> ReceiveSlashVersion(
           [HttpTrigger(AuthorizationLevel.Function, "post", Route = "receive/slash/version")]HttpRequestMessage req,
           ILogger logger)
        {
            if (Settings.Debug)
                // Trim off the beginning because of AI trying to "help"
                logger.LogInformation("Body: {0}", (await req.Content.ReadAsStringAsync()).Substring(10));

            // Make sure it's a legit request
            if (!await req.IsAuthed(logger))
                return req.CreateErrorResponse(HttpStatusCode.Unauthorized, "Did not match hash.");

            // Return the version
            return req.CreateResponse(HttpStatusCode.OK, new { response_type = "in_channel", text = $"```{typeof(SlackEntry).Assembly.ManifestModule.Name} v{GitVersionInformation.SemVer}```" });
        }

        [FunctionName(nameof(Ping))]
        public static HttpResponseMessage Ping([HttpTrigger(AuthorizationLevel.Function, "get", Route = "ping")]HttpRequestMessage req)
        {
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [FunctionName(nameof(Debug))]
        public static async Task<HttpResponseMessage> Debug([HttpTrigger(AuthorizationLevel.Function, "post", Route = "debug")]HttpRequestMessage req)
        {
            var thing = await req.Content.ReadAsFormDataAsync<Slack.Slash>();

            return req.CreateResponse(HttpStatusCode.OK, thing);
        }
    }
}
