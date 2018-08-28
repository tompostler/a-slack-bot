namespace Slack.Types
{
    /// <summary>
    /// A user object contains information about a member.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/types/user on 2018-08-28
    /// </remarks>
    public class user
    {
        /// <summary>
        /// The <see cref="id"/> field is a string identifier for this team member. It is only unique to the
        /// workspace/team containing the user. Use this field instead of the <see cref="name"/> field when storing
        /// related data or when specifying the user in API requests. Though the ID field usually begins with U, it is
        /// also possible to encounter user IDs beginning with W. We recommend considering the string an opaque value. 
        /// </summary>
        public string id { get; set; }

        public string team_id { get; set; }

        /// <summary>
        /// For deactivated users, <see cref="deleted"/> will be true.
        /// </summary>
        public bool deleted { get; set; }

        /// <summary>
        /// The <see cref="color"/> field is used in some clients to display a colored username.
        /// </summary>
        public string color { get; set; }

        public string real_name { get; set; }

        /// <summary>
        /// <see cref="tz"/> provides a somewhat human readable string for the geographic region like
        /// "America/Los_Angels".
        /// </summary>
        public string tz { get; set; }

        /// <summary>
        /// <see cref="tz_label"/> is a string describing the name of that timezone (like
        /// "Pacific Standard Time").
        /// </summary>
        public string tz_label { get; set; }

        /// <summary>
        /// <see cref="tz_offset"/> is a signed integer indicating the number of seconds to offset UTC time by.
        /// </summary>
        public int tz_offset { get; set; }

        /// <summary>
        /// The profile object contains as much information as the user has supplied in the default profile fields.
        /// </summary>
        public user_parts.profile profile { get; set; }

        public bool is_admin { get; set; }
        public bool is_owner { get; set; }
        public bool is_primary_owner { get; set; }

        /// <summary>
        /// <see cref="is_restricted"/> indicates the user is a multi-channel guest.
        /// </summary>
        public bool is_restricted { get; set; }

        /// <summary>
        /// <see cref="is_ultra_restriced"/> indicates they are a single channel guest.
        /// </summary>
        public bool is_ultra_restriced { get; set; }

        public bool is_bot { get; set; }
        public bool is_stranger { get; set; }

        /// <summary>
        /// The <see cref="updated"/> field is a unix timestamp when the user was last updated.
        /// </summary>
        public long updated { get; set; }

        public bool is_app_user { get; set; }

        /// <summary>
        /// The <see cref="has_2fa"/> field describes whether two-step verification is enabled for this user. This
        /// field will always be displayed if you are looking at your own user information. If you are looking at
        /// another user's information this field will only be displayed if you are Workspace Admin or owner.
        /// </summary>
        public bool has_2fa { get; set; }

        /// <summary>
        /// The <see cref="two_factor_type"/> field is either 'app' or 'sms'. It will only be present if
        /// <see cref="has_2fa"/> is true.
        /// </summary>
        public string two_factor_type { get; set; }

        /// <summary>
        /// The locale field is a string containing a IETF language code, such as 'en-US'.
        /// </summary>
        /// <remarks>
        /// https://api.slack.com/changelog/2017-09-locale-locale-locale
        /// https://en.wikipedia.org/wiki/IETF_language_tag
        /// </remarks>
        public string locale { get; set; }

    }
}
