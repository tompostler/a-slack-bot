﻿using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SBSend
    {
        private static readonly HttpClient httpClient = new HttpClient();
        static SBSend()
        {
            if (!string.IsNullOrWhiteSpace(Settings.SlackOauthBotToken))
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.SlackOauthBotToken);
        }

        [FunctionName(nameof(SBSendMessage))]
        public static async Task SBSendMessage(
            [ServiceBusTrigger(C.SBQ.SendMessage)]Slack.Events.Inner.message messageData,
            ILogger logger)
        {
            await SendMessage(messageData, logger);
        }

        // This is pulled into a separate message to ease message threading scenarios.
        public static async Task<Slack.WebAPIResponse> SendMessage(Slack.Events.Inner.message message, ILogger logger)
        {
            var response = await httpClient.PostAsJsonAsync("https://slack.com/api/chat.postMessage", message);
            logger.LogInformation("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());

            var responseContent = await response.Content.ReadAsAsync<Slack.WebAPIResponse>();
            if (!responseContent.ok)
                switch (responseContent.error)
                {
                    // These are fine in this scenario
                    case "not_in_channel":
                        break;

                    case "app_rate_limited":
                        await Task.Delay(response.Headers.RetryAfter.Delta ?? TimeSpan.FromSeconds(5));
                        throw new Exception("Retry-After; requeue with ServiceBus");

                    default:
                        throw new Exception("Slack API Error: " + responseContent.error);
                }

            return responseContent;
        }
    }
}