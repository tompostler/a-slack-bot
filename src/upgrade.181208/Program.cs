using a_slack_bot;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace upgrade._181208
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var dc = new DocumentClient(new Uri("https://aslackbot.documents.azure.com:443/"), args[0]);

            var query = dc.CreateDocumentQuery<a_slack_bot.Documents2.Blackjack>(
                UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                "SELECT * FROM c WHERE c.TypeSubtype = 'Game|Blackjack' AND c.id <> 'BlackjackStandings'",
                new FeedOptions { EnableCrossPartitionQuery = true })
                .AsDocumentQuery();

            // Just load it all in memory. Should be less than a few hundred MBs
            List<a_slack_bot.Documents2.Blackjack> results = new List<a_slack_bot.Documents2.Blackjack>();
            while (query.HasMoreResults)
            {
                results.AddRange(await query.ExecuteNextAsync<a_slack_bot.Documents2.Blackjack>());
                Console.WriteLine("Found {0} things to migrate...", results.Count);
                await Task.Delay(TimeSpan.FromSeconds(0.5));
                // Batch it 1k at a time
                if (results.Count >= 1_000)
                    break;
            }
            Console.WriteLine("Found {0} things to migrate.", results.Count);
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Fan them all out, 10 at a time
            var ss = new SemaphoreSlim(10);
            int i = 0;
            var tasks = results.Select(async (doc) =>
            {
                await ss.WaitAsync();

                await ExecuteWithIndefiniteRetries(doc.Id as string, async () =>
                {
                    await dc.UpsertDocumentAsync(C.CDB2.CUs[C.CDB2.Col.GamesBlackjack], doc, new RequestOptions { PartitionKey = doc.PK });
                    await dc.DeleteDocumentAsync(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, $"{doc.channel_id}|{doc.thread_ts}"), new RequestOptions { PartitionKey = new PartitionKey("Game|Blackjack") });
                });
                Console.WriteLine("{0}: {1}", Interlocked.Increment(ref i), doc.Id);

                ss.Release();
            }).ToList();
            await Task.WhenAll(tasks);

            // Move the standings manually
        }

        private static async Task ExecuteWithIndefiniteRetries(string oldId, Func<Task> func)
        {
            while (true)
                try
                {
                    await func();
                    break;
                }
                catch (DocumentClientException dce) when ((int)dce.StatusCode == 429)
                {
                    Console.WriteLine("DELAY: {0}", oldId);
                    await Task.Delay(dce.RetryAfter);
                }
                catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
                {
                    break;
                }
        }
    }
}
