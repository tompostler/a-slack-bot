using System;

namespace Slack.Types.user_parts
{
    /// <summary>
    /// The profile object contains as much information as the user has supplied in the default profile fields.
    /// </summary>
    /// <remarks>
    /// https://api.slack.com/methods/users.profile.get on 2018-08-28
    /// </remarks>
    public class profile
    {
        public string avatar_hash { get; set; }

        /// <summary>
        /// The user's custom-set "current status"
        /// </summary>
        public string status_text { get; set; }

        /// <summary>
        /// The user's custom-set "current status"
        /// </summary>
        public string status_emoji { get; set; }

        public string title { get; set; }
        public string phone { get; set; }
        public string skype { get; set; }
        public string real_name { get; set; }
        public string display_name { get; set; }

        /// <summary>
        /// <see cref="display_name_normalized"/> and <see cref="real_name_normalized"/> filter out any non-Latin
        /// characters typically allowed in <see cref="display_name"/> and <see cref="real_name"/>.
        /// </summary>
        public string real_name_normalized { get; set; }

        /// <summary>
        /// <see cref="display_name_normalized"/> and <see cref="real_name_normalized"/> filter out any non-Latin
        /// characters typically allowed in <see cref="display_name"/> and <see cref="real_name"/>.
        /// </summary>
        public string display_name_normalized { get; set; }

        /// <summary>
        /// The <c>users:read.email</c> OAuth scope is now required to access the <see cref="email"/> field in user
        /// objects returned by the <c>users.list</c> and <c>users.info</c> web API methods. <c>users:read</c> is no
        /// longer a sufficient scope for this data field.
        /// </summary>
        public string email { get; set; }

        /// <summary>
        /// The image_ keys hold links to the different sizes we support for the user's profile image from 24x24 to
        /// 1024x1024 pixels. A link to the image in its original size is stored in <see cref="image_original"/>.
        /// </summary>
        public Uri image_24 { get; set; }

        /// <summary>
        /// The image_ keys hold links to the different sizes we support for the user's profile image from 24x24 to
        /// 1024x1024 pixels. A link to the image in its original size is stored in <see cref="image_original"/>.
        /// </summary>
        public Uri image_32 { get; set; }

        /// <summary>
        /// The image_ keys hold links to the different sizes we support for the user's profile image from 24x24 to
        /// 1024x1024 pixels. A link to the image in its original size is stored in <see cref="image_original"/>.
        /// </summary>
        public Uri image_48 { get; set; }

        /// <summary>
        /// The image_ keys hold links to the different sizes we support for the user's profile image from 24x24 to
        /// 1024x1024 pixels. A link to the image in its original size is stored in <see cref="image_original"/>.
        /// </summary>
        public Uri image_72 { get; set; }

        /// <summary>
        /// The image_ keys hold links to the different sizes we support for the user's profile image from 24x24 to
        /// 1024x1024 pixels. A link to the image in its original size is stored in <see cref="image_original"/>.
        /// </summary>
        public Uri image_192 { get; set; }

        /// <summary>
        /// The image_ keys hold links to the different sizes we support for the user's profile image from 24x24 to
        /// 1024x1024 pixels. A link to the image in its original size is stored in <see cref="image_original"/>.
        /// </summary>
        public Uri image_512 { get; set; }

        /// <summary>
        /// The image_ keys hold links to the different sizes we support for the user's profile image from 24x24 to
        /// 1024x1024 pixels. A link to the image in its original size is stored in <see cref="image_original"/>.
        /// </summary>
        public Uri image_1024 { get; set; }

        /// <summary>
        /// The image_ keys hold links to the different sizes we support for the user's profile image from 24x24 to
        /// 1024x1024 pixels. A link to the image in its original size is stored in <see cref="image_original"/>.
        /// </summary>
        public Uri image_original { get; set; }

        public string team { get; set; }
        public bool is_custom_image { get; set; }

        /// <summary>
        /// Bot users may contain this profile field, indicating whether the bot user is active in a way that overrides
        /// traditional presence rules.
        /// </summary>
        public bool always_active { get; set; }
    }
}
