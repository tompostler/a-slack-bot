﻿using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static class SBEvent
    {
        private static readonly HttpClient httpClient = new HttpClient();

        [FunctionName(nameof(SBReceiveEvent))]
        public static async Task SBReceiveEvent(
            [ServiceBusTrigger(Constants.SBQ.InputEvent)]Messages.ServiceBusInputEvent eventMessage,
            [DocumentDB(Constants.CDB.DN, Constants.CDB.CN, ConnectionStringSetting = Constants.CDB.CSS, CreateIfNotExists = true)]IAsyncCollector<Documents.Event> documentCollector,
            [ServiceBus(Constants.SBQ.SendMessage)]IAsyncCollector<Slack.Events.Inner.message> messageCollector,
            ILogger logger)
        {
            // First, send it to cosmos for the records
            await documentCollector.AddAsync(new Documents.Event { Content = eventMessage.@event });

            // Then, do something with it
        }
    }
}