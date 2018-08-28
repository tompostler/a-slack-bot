using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace a_slack_bot
{
    public static class Extensions
    {
        public static string ToHexString(this byte[] @this)
        {
            return BitConverter.ToString(@this).Replace("-", "").ToLower();
        }

        public static async Task<bool> IsAuthed(this HttpRequestMessage @this, ILogger logger)
        {
            var hasher = new HMACSHA256(Settings.SlackSigningSecretBytes);
            var hashComputed = "v0=" + hasher.ComputeHash(Encoding.UTF8.GetBytes($"v0:{@this.Headers.GetValues(C.Headers.Slack.RequestTimestamp).First()}:{await @this.Content.ReadAsStringAsync()}")).ToHexString();
            var hashExpected = @this.Headers.GetValues(C.Headers.Slack.Signature).First();
            logger.LogInformation("Sig check; Computed:{0} Expected:{1}", hashComputed, hashExpected);
            return hashComputed == hashExpected;
        }

        public static async Task<T> ReadAsFormDataAsync<T>(this HttpContent @this)
            where T : new()
        {
            var formData = await @this.ReadAsFormDataAsync();

            T t = default;
            foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // If the form data doesn't have the property, then skip it
                if (!formData.AllKeys.Contains(property.Name))
                    continue;

                // Have to make sure we can read/write the property
                if (!property.CanRead || !property.CanWrite)
                    continue;

                // Get and set methods have to be public
                MethodInfo mget = property.GetGetMethod(false);
                MethodInfo mset = property.GetSetMethod(false);
                if (mget == null || mset == null)
                    continue;

                // Initialize (if necessary) and set the property
                if (t == default)
                    t = new T();
                property.SetValue(t, formData[property.Name]);
            }

            return t;
        }
    }
}
