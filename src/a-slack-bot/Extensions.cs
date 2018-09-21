using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

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

        public static Task<HttpResponseMessage> PostAsFormDataAsync(this HttpClient @this, string requestUri, Dictionary<string, string> formDatas)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var formData in formDatas)
            {
                sb.Append(HttpUtility.UrlEncode(formData.Key));
                sb.Append('=');
                sb.Append(HttpUtility.UrlEncode(formData.Value));
                sb.Append('&');
            }
            sb.Remove(sb.Length - 1, 1);
            return @this.PostAsync(requestUri, new StringContent(sb.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded"));
        }

        public static async Task<IEnumerable<T>> GetAllResults<T>(this IDocumentQuery<T> documentQuery, ILogger logger)
        {
            logger.LogInformation("Executing document query...");
            var i = 0;
            var results = new List<T>();
            while (documentQuery.HasMoreResults)
            {
                logger.LogInformation("Getting page {0}", ++i);
                foreach (T result in await documentQuery.ExecuteNextAsync<T>())
                {
                    results.Add(result);
                }
            }
            logger.LogInformation("Executed document query. {0} results found.", results.Count);
            return results;
        }

        public static string ToBase33String(this long @this)
        {
            const string base33Alphabet = "0123456789ABCDEFGHJKLMNPQRSTUVXYZ";
            string basic = string.Empty;

            do
            {
                long remainder = @this % base33Alphabet.Length;
                basic = base33Alphabet[(int)remainder] + basic;
                @this = (@this - remainder) / base33Alphabet.Length;
            }
            while (@this > 0);

            return basic;
        }
    }
}
