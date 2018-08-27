using a_slack_bot.Messages;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace a_slack_bot
{
    public static class EntryFunctions
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

        private static HttpClient httpClient = new HttpClient();
        [FunctionName(nameof(ReceiveSlash))]
        public static async Task<HttpResponseMessage> ReceiveSlash(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "receive/slash")]HttpRequestMessage req,
            [ServiceBus(Constants.SBQ.InputSlash, AccessRights.Manage)]IAsyncCollector<ServiceBusInputSlash> messageCollector,
            ILogger logger)
        {
            if (Settings.Debug)
                logger.LogInformation("Body: {0}", await req.Content.ReadAsStringAsync());

            // Make sure it's a legit request
            if (!await req.IsAuthed(logger))
                return req.CreateErrorResponse(HttpStatusCode.Unauthorized, "Did not match hash.");

            // Get stuff from the message
            var slashData = await req.Content.ReadAsFormDataAsync<Slack.Slash>();

            // In order to not echo the slash command back into the channel, we need to respond right away
            //DEBUG HACK TEST THING
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Settings.SlackOauthToken);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Slack-User", "U1DPNJM0F");
            var response = await httpClient.PostAsJsonAsync(slashData.response_url, new
            {
                channel = slashData.channel_id,
                text = "SPACES SPACES SPACES"
            });
            logger.LogInformation("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());

            // Send it off to be processed
            logger.LogInformation("Sending slash command into the queue.");
            await messageCollector.AddAsync(new ServiceBusInputSlash
            {
                slash = slashData
            });

            // Return all is well
            return req.CreateResponse(HttpStatusCode.OK, new { response_type = "ephemeral", text = "HERE GOES NOTHIN" });
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
