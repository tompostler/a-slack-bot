using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static class SBSend
    {
        private static readonly HttpClient httpClient = new HttpClient();
        static SBSend()
        {
            if (!string.IsNullOrWhiteSpace(Settings.SlackOauthBotToken))
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.SlackOauthBotToken);
        }

        [FunctionName(nameof(SBSendReaction))]
        public static async Task SBSendReaction(
            [ServiceBusTrigger(C.SBQ.SendReaction)]Messages.ReactionAdd messageData,
            ILogger logger)
        {
            if (Settings.Debug)
                logger.LogInformation(JsonConvert.SerializeObject(messageData));

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
            if (Settings.Debug)
                logger.LogInformation(JsonConvert.SerializeObject(messageData));

            return SendMessage(messageData, logger);
        }

        // This is pulled into a separate message to ease message threading scenarios.
        public static async Task<Slack.WebAPIResponse> SendMessage(Slack.Events.Inner.message message, ILogger logger)
        {
            // Convert to the bare contract for sending the message
            var sendableMessage = new
            {
                message.channel,
                message.as_user,
                message.attachments,
                message.blocks,
                message.reply_broadcast,
                // Rewrite anything point to blob storage to use the cdn instead
                text = message.text?.Replace(Settings.BlobsSourceHostname, Settings.BlobsTargetHostname),
                message.thread_ts,
                message.ts,
                message.user
            };

            HttpResponseMessage response;
            if (string.IsNullOrWhiteSpace(sendableMessage.ts))
                response = await httpClient.PostAsJsonAsync("https://slack.com/api/chat.postMessage", sendableMessage);
            else
            {
                logger.LogInformation("Updating message {0}", sendableMessage.ts);
                sendableMessage = new
                {
                    sendableMessage.channel,
                    as_user = true,
                    sendableMessage.attachments,
                    sendableMessage.blocks,
                    sendableMessage.reply_broadcast,
                    sendableMessage.text,
                    sendableMessage.thread_ts,
                    sendableMessage.ts,
                    sendableMessage.user
                };
                response = await httpClient.PostAsJsonAsync("https://slack.com/api/chat.update", sendableMessage);
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
            [ServiceBusTrigger(C.SBQ.SendMessageEphemeral)]Slack.Events.Inner.message message,
            [ServiceBus(C.SBQ.SendMessage)]IAsyncCollector<object> messageCollector,
            ILogger logger)
        {
            await SR.Init(logger);

            // Convert to the bare contract for sending the message
            var sendableMessage = new
            {
                message.channel,
                message.as_user,
                message.attachments,
                message.blocks,
                message.reply_broadcast,
                // Rewrite anything point to blob storage to use the cdn instead
                text = message.text?.Replace(Settings.BlobsSourceHostname, Settings.BlobsTargetHostname),
                message.thread_ts,
                message.ts,
                message.user
            };

            if (SR.C.IdToConversation.ContainsKey(sendableMessage.channel) && (SR.C.IdToConversation[sendableMessage.channel].is_mpim || SR.C.IdToConversation[sendableMessage.channel].is_im))
            {
                logger.LogInformation("Message is in a private channel. Posting as regular message instead.");
                await messageCollector.AddAsync(sendableMessage);
                return;
            }

            var response = await httpClient.PostAsJsonAsync("https://slack.com/api/chat.postEphemeral", sendableMessage);
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
