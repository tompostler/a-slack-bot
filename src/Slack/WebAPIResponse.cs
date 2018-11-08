using System.Collections.Generic;

namespace Slack
{
    /// <summary>
    /// The Slack Web API is an interface for querying information from and enacting change in a Slack workspace.
    /// 
    /// All Web API responses contain a JSON object, which will always contain a top-level boolean property
    /// <see cref="ok"/>, indicating success or failure. On failure, <see cref="warning"/> and <see cref="error"/> may
    /// also be set.
    /// 
    /// Other properties are defined in the documentation for each relevant method.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/web on 2018-08-29
    /// </remarks>
    public class WebAPIResponse
    {
        /// <summary>
        /// All Web API responses contain a JSON object, which will always contain a top-level boolean property
        /// <see cref="ok"/>, indicating success or failure. On failure, <see cref="warning"/> and <see cref="error"/>
        /// may also be set.
        /// </summary>
        public bool ok { get; set; }

        /// <summary>
        /// In the case of problematic calls that could still be completed successfully, <see cref="ok"/> will be true
        /// and the <see cref="warning"/> property will contain a short machine-readable warning code (or
        /// comma-separated list of them, in the case of multiple warnings).
        /// </summary>
        public string warning { get; set; }

        /// <summary>
        /// For failure results, the <see cref="error"/> property will contain a short machine-readable error code. 
        /// </summary>
        public string error { get; set; }

        /// <summary>
        /// users.list
        /// </summary>
        public List<Types.user> members { get; set; }

        /// <summary>
        /// chat.postMessage
        /// </summary>
        public string channel { get; set; }

        /// <summary>
        /// conversations.list
        /// </summary>
        public List<Types.converation> channels { get; set; }

        /// <summary>
        /// chat.postMessage
        /// </summary>
        public Events.Inner.message message { get; set; }
    }
}
