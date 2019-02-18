using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
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

        [FunctionName(nameof(SBSendReaction))]
        public static async Task SBSendReaction(
            [ServiceBusTrigger(C.SBQ.SendReaction)]Messages.ServiceBusReactionAdd messageData,
            [DocumentDB(ConnectionStringSetting = C.CDB.CSS)]DocumentClient docClient,
            ILogger logger)
        {
            var response = await httpClient.PostAsJsonAsync("https://slack.com/api/reactions.add", messageData);
            logger.LogInformation("{0}: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());

            var responseContent = await response.Content.ReadAsAsync<Slack.WebAPIResponse>();
            if (!responseContent.ok)
                switch (responseContent.error)
                {
                    // These are fine in this scenario
                    case "already_reacted":
                    case "too_many_emoji":
                    case "too_many_reactions":
                        break;

                    case "invalid_name":
                        // TODO: Purge from cosmos
                        logger.LogError("invalid_name: {0}", messageData.name);
                        break;

                    case "app_rate_limited":
                        await Task.Delay(response.Headers.RetryAfter.Delta ?? TimeSpan.FromSeconds(5));
                        throw new Exception("Retry-After; requeue with ServiceBus");

                    default:
                        throw new Exception("Slack API Error: " + responseContent.error);
                }
        }

        [FunctionName(nameof(SBSendMessage))]
        public static Task SBSendMessage(
            [ServiceBusTrigger(C.SBQ.SendMessage)]Slack.Events.Inner.message messageData,
            ILogger logger)
        {
            return SendMessage(messageData, logger);
        }

        // This is pulled into a separate message to ease message threading scenarios.
        public static async Task<Slack.WebAPIResponse> SendMessage(Slack.Events.Inner.message message, ILogger logger)
        {
            HttpResponseMessage response = null;
            if (string.IsNullOrWhiteSpace(message.ts))
                response = await httpClient.PostAsJsonAsync("https://slack.com/api/chat.postMessage", message);
            else
            {
                logger.LogInformation("Updating message {0}", message.ts);
                message.as_user = true;
                response = await httpClient.PostAsJsonAsync("https://slack.com/api/chat.update", message);
            }
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

        [FunctionName(nameof(SBSendMessageEphemeral))]
        public static async Task SBSendMessageEphemeral(
            [ServiceBusTrigger(C.SBQ.SendMessageEphemeral)]Slack.Events.Inner.message messageData,
            [ServiceBus(C.SBQ.SendMessage)]IAsyncCollector<Slack.Events.Inner.message> messageCollector,
            ILogger logger)
        {
            await SR.Init(logger);
            if (SR.C.IdToConversation.ContainsKey(messageData.channel) && (SR.C.IdToConversation[messageData.channel].is_mpim || SR.C.IdToConversation[messageData.channel].is_im))
            {
                logger.LogInformation("Message is in a private channel. Posting as regular message instead.");
                messageData.user = null;
                await messageCollector.AddAsync(messageData);
                return;
            }

            var response = await httpClient.PostAsJsonAsync("https://slack.com/api/chat.postEphemeral", messageData);
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
        }
    }
}
