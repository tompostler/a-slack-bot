using System;

namespace Slack
{
    /// <summary>
    /// When a slash command is invoked, Slack sends an HTTP POST to the Request URL you specified above. This request
    /// contains a data payload describing the source command and who invoked it, like a really detailed knock at the
    /// door.
    /// 
    /// This data will be sent with a Content-type header set as application/x-www-form-urlencoded. 
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/slash-commands on 2018-08-26
    /// </remarks>
    public class Slash
    {
        /// <summary>
        /// The command that was typed in to trigger this request. This value can be useful if you want to use a single
        /// Request URL to service multiple Slash Commands, as it lets you tell them apart.
        /// </summary>
        public string command { get; set; }

        /// <summary>
        /// This is the part of the Slash Command after the command itself, and it can contain absolutely anything that
        /// the user might decide to type. It is common to use this text parameter to provide extra context for the
        /// command. 
        /// </summary>
        public string text { get; set; }

        /// <summary>
        /// A URL that you can use to respond to the command.
        /// </summary>
        public string response_url { get; set; }

        /// <summary>
        /// If you need to respond to the command by opening a dialog, you'll need this trigger ID to get it to work.
        /// You can use this ID with dialog.open up to 3000ms after this data payload is sent.
        /// </summary>
        public string trigger_id { get; set; }

        /// <summary>
        /// The ID of the user who triggered the command.
        /// </summary>
        public string user_id { get; set; }

        /// <summary>
        /// These IDs provide context about where the user was in Slack when they triggered your app's command (eg.
        /// which workspace, Enterprise Grid, or channel). You may need these IDs for your command response. 
        /// </summary>
        public string team_id { get; set; }

        /// <summary>
        /// These IDs provide context about where the user was in Slack when they triggered your app's command (eg.
        /// which workspace, Enterprise Grid, or channel). You may need these IDs for your command response. 
        /// </summary>
        public string channel_id { get; set; }
    }
}
