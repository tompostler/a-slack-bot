using System.Collections.Generic;

namespace Slack.Types
{
    /// <summary>
    /// A  conversation object contains information about a channel-like thing in Slack. It might be a public channel,
    /// a private channel, a direct message, or a multi-person direct message.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/types/conversation on 2018-11-08
    /// </remarks>
    public class converation
    {
        public string id { get; set; }

        /// <summary>
        /// Indicates the name of the channel-like thing, without a leading hash sign. Don't get too attached to that
        /// name. It might change. Don't bother storing it even. When thinking about channel-like things, think about
        /// their IDs and their type and the team/workspace they belong to.
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Indicates whether a conversation is a public channel. Everything said in a public channel can be read by
        /// anyone else belonging to a workspace. <see cref="is_private"/> will be false. Check both just to be sure,
        /// why not?
        /// </summary>
        public bool is_channel { get; set; }

        /// <summary>
        /// Means the channel is a private channel. <see cref="is_private"/> will also be true. Check yourself before
        /// you wreck yourself.
        /// </summary>
        public bool is_group { get; set; }

        /// <summary>
        /// Means the conversation is a direct message between two distinguished individuals or a user and a bot. Yes,
        /// it's an <see cref="is_private"/> conversation.
        /// </summary>
        public bool is_im { get; set; }

        /// <summary>
        /// A unix timestamp.
        /// </summary>
        public long created { get; set; }

        /// <summary>
        /// The user ID of the member that created this channel.
        /// </summary>
        public string creator { get; set; }

        /// <summary>
        /// Indicates a conversation is archived. Frozen in time.
        /// </summary>
        public bool is_archived { get; set; }

        /// <summary>
        /// Means the channel is the workspace's "general" discussion channel (even if it's not named #general but it
        /// might be anyway). That might be important to your app because almost every user is a member.
        /// </summary>
        public bool is_general { get; set; }

        public int unlinked { get; set; } //?

        public string name_normalized { get; set; }

        /// <summary>
        /// Means the conversation can't be written to by typical users. Admins may have the ability.
        /// </summary>
        public bool is_read_only { get; set; }

        /// <summary>
        /// Means the conversation is in some way shared between multiple workspaces. Look for
        /// <see cref="is_ext_shared"/> and <see cref="is_org_shared"/> to learn which kind it is, and if that matters,
        /// act accordingly. Have we mentioned how great <see cref="is_private"/> is yet?
        /// </summary>
        public bool is_shared { get; set; }

        /// <summary>
        /// Represents this conversation as being part of a Shared Channel with a remote organization. Your app should
        /// make sure the data it shares in such a channel is appropriate for both workspaces. <see cref="is_shared"/>
        /// will also be true.
        /// </summary>
        public bool is_ext_shared { get; set; }

        /// <summary>
        /// Explains whether this shared channel is shared between Enterprise Grid workspaces within the same
        /// organization. It's a little different from (externally) Shared Channels. Yet, <see cref="is_shared"/> will
        /// be true.
        /// </summary>
        public bool is_org_shared { get; set; }

        public HashSet<string> pending_shared { get; set; }

        /// <summary>
        /// Is intriguing. It means the conversation is ready to become an <see cref="is_ext_shared"/> channel but
        /// isn't quite ready yet and needs some kind of approval or sign off. Best to treat it as if it were a
        /// shared channel, even if it traverses only one workspace.
        /// </summary>
        public bool is_pending_ext_shared { get; set; }

        /// <summary>
        /// Indicates the user or bot user or Slack app associated with the token making the API call is itself a
        /// member of the conversation.
        /// </summary>
        public bool is_member { get; set; }

        /// <summary>
        /// Means the conversation is privileged between two or more members. Meet their privacy expectations.
        /// </summary>
        public bool is_private { get; set; }

        /// <summary>
        /// Represents an unnamed private conversation between multiple users. It's an <see cref="is_private"/> kind of
        /// thing.
        /// </summary>
        public bool is_mpim { get; set; }

        /// <summary>
        /// The timestamp for the last message the calling user has read in this channel.
        /// </summary>
        public string last_read { get; set; }

        /// <summary>
        /// Provide information about the channel topic.
        /// </summary>
        public conversation_parts.topicpurpose topic { get; set; }

        /// <summary>
        /// Provide information about the channel purpose.
        /// </summary>
        public conversation_parts.topicpurpose purpose { get; set; }

        public List<string> previous_names { get; set; }

        public int num_members { get; set; }

        public string locale { get; set; }

        public string user { get; set; }
    }
}
