using a_slack_bot.Messages;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SlackEntry
    {
        [FunctionName(nameof(ReceiveEvent))]
        public static async Task<HttpResponseMessage> ReceiveEvent(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "receive/event")]HttpRequestMessage req,
            [ServiceBus(C.SBQ.InputEvent)]IAsyncCollector<BrokeredMessage> messageCollector,
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
                var @event = ((Slack.Events.Outer.event_callback)outerEvent).@event;
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new ServiceBusInputEvent { @event = @event })), writable: false);
                var msg = new BrokeredMessage(stream, ownsStream: true)
                {
                    ContentType = "application/json",
                    MessageId = @event.event_ts
                };
                logger.LogInformation("Sending inner event into the queue with id {0}.", msg.MessageId);
                await messageCollector.AddAsync(msg);
            }

            // Return all is well
            return req.CreateResponse(HttpStatusCode.OK);
        }

        private static readonly HashSet<string> EchoableCommands = new HashSet<string>
        {
            "/asb-whitelist",
            "/blackjack"
        };
        [FunctionName(nameof(ReceiveSlash))]
        public static async Task<HttpResponseMessage> ReceiveSlash(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "receive/slash")]HttpRequestMessage req,
            [ServiceBus(C.SBQ.InputSlash, AccessRights.Manage)]IAsyncCollector<ServiceBusInputSlash> messageCollector,
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
            if (EchoableCommands.Contains(slashData.command))
                return req.CreateResponse(HttpStatusCode.OK, new { response_type = "in_channel" });
            else
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
            var thing = await req.Content.ReadAsFormDataAsync<Slack.Slash>();

            return req.CreateResponse(HttpStatusCode.OK, thing);
        }
    }
}
