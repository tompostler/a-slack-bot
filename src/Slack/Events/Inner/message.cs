using System.Collections.Generic;

namespace Slack.Events.Inner
{
    /// <summary>
    /// A message was sent to a channel
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/events/message on 2018-08-26
    /// </remarks>
    public class message : EventBase
    {
        public message()
        {
            this.type = nameof(message);
        }

        /// <summary>
        /// The ID of the channel, private group or DM channel this message is posted in
        /// </summary>
        public string channel { get; set; }

        /// <summary>
        /// The ID of the user speaking
        /// </summary>
        public string user { get; set; }

        /// <summary>
        /// The text spoken
        /// </summary>
        public string text { get; set; }

        /// <summary>
        /// The unique (per-channel) timestamp
        /// </summary>
        public string ts { get; set; }

        /// <summary>
        /// The <see cref="ts"/> that started the thread. If a message has a <see cref="thread_ts"/> value, then it is
        /// part of a threaded conversation.
        /// </summary>
        public string thread_ts { get; set; }

        /// <summary>
        /// Used in conjunction with <see cref="thread_ts"/> and indicates whether reply should be made visible to
        /// everyone in the channel or conversation. Defaults to false.
        ///
        /// When your reply is broadcast to the channel, it'll actually be a reference to your reply, not the reply
        /// itself. So, when appearing in the channel, it won't contain any attachments or message buttons.
        /// </summary>
        public bool reply_broadcast { get; set; }

        /// <summary>
        /// Attachments let you add more context to a message, making them more useful and effective.
        /// 
        /// Please limit your messages to contain no more than 20 attachments to provide the best user experience.
        /// Whenever possible, we'll throw a <c>too_many_attachments</c> error when attempting to include more than 100
        /// attachments. When using incoming webhooks, you'll receive that error as a HTTP 400.
        /// </summary>
        public List<message_parts.attachment> attachments { get; set; }

        /// <summary>
        /// If the message has been edited after posting it will include this property, including the user ID of the
        /// editor, and the timestamp the edit happened. The original text of the message is not available.
        /// </summary>
        public message_parts.edited edited { get; set; }

        /// <summary>
        /// Unlike other event types, message events can have a subtype.
        /// 
        /// They are structured in this way so that clients can either support them fully by having distinct behavior
        /// for each different subtype, or can fallback to a simple mode by just displaying the text of the message.
        /// </summary>
        /// <remarks>
        /// The full list of message subtypes supported by the Events API is:
        /// bot_message         A message was posted by an integration
        /// me_message          A /me message was sent
        /// message_changed     A message was changed
        /// message_deleted     A message was deleted
        /// message_replied     A message thread received a reply
        /// thread_broadcast    A message thread's reply was broadcast to a channel
        /// </remarks>
        public string subtype { get; set; }

        /// <summary>
        /// Some subtypes have a special hidden property. These indicate messages that are part of the history of a
        /// channel but should not be displayed to users.
        /// </summary>
        public bool hidden { get; set; }

        /// <summary>
        /// This property is present and true if the calling user has starred the message, else it is omitted.
        /// </summary>
        public bool is_starred { get; set; }

        /// <summary>
        /// This array, if present, contains the IDs of any channels to which the message is currently pinned.
        /// </summary>
        public List<string> pinned_to { get; set; }

        /// <summary>
        /// This property, if present, contains any reactions that have been added to the message and gives information
        /// about the type of reaction, the total number of users who added that reaction and a (possibly incomplete)
        /// list of users who have added that reaction to the message. 
        /// </summary>
        public List<message_parts.reaction> reactions { get; set; }

        // The following are optional properties set when using a specific message subtype

        /// <summary>
        /// This tells you which bot sent this message.
        /// </summary>
        /// <remarks>
        /// https://api.slack.com/events/message/bot_message on 2018-08-27
        /// </remarks>
        public string bot_id { get; set; }

        // The following are optional properties set when using this to post messages

        /// <summary>
        /// Pass true to post the message as the authed user, instead of as a bot. 
        /// </summary>
        /// <remarks>
        /// https://api.slack.com/methods/chat.postMessage
        /// </remarks>
        public bool as_user { get; set; }
    }
}
