using System.Collections.Generic;

namespace Slack.Events.Inner.message_parts
{
    /// <summary>
    /// This contains any reactions that have been added to the message and gives information about the type of
    /// reaction, the total number of users who added that reaction and a (possibly incomplete) list of users who have
    /// added that reaction to the message.
    /// 
    /// The users array in the reactions property might not always contain all users that have reacted (we limit it to
    /// X users, and X might change), however count will always represent the count of all users who made that reaction
    /// (i.e. it may be greater than users.length). If the authenticated user has a given reaction then they are
    /// guaranteed to appear in the users array, regardless of whether count is greater than users.length or not.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/events/message on 2018-08-26
    /// </remarks>
    public class reaction
    {
        /// <summary>
        /// The name of the reaction.
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// The count of all users who made that reaction (i.e. it may be greater than <see cref="users"/>.Length).
        /// </summary>
        public int count { get; set; }

        /// <summary>
        /// This property might not always contain all users that have reacted (we limit it to X users, and X might
        /// change). If the authenticated user has a given reaction then they are guaranteed to appear in the array.
        /// </summary>
        public List<string> users { get; set; }
    }
}
