using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace upgrade._181008
{
    /// <summary>
    /// Migrate all previous documents with Type == 'Event' to have ids matching their 'event_ts'.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            var dc = new DocumentClient(new Uri("https://aslackbot.documents.azure.com:443/"), args[0]);

            var query = dc.CreateDocumentQuery(
                UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                "SELECT * FROM c WHERE c.Type = 'Event' AND c.id <> c.Content.event_ts",
                new FeedOptions { EnableCrossPartitionQuery = true })
                .AsDocumentQuery();

            // Just load it all in memory. Should be less than a few hundred MBs
            List<dynamic> results = new List<dynamic>();
            while (query.HasMoreResults)
            {
                results.AddRange(await query.ExecuteNextAsync());
                Console.WriteLine("Found {0} things to update...", results.Count);
                await Task.Delay(TimeSpan.FromSeconds(0.5));
                // Batch it 2k at a time
                if (results.Count > 2_000)
                    break;
            }
            Console.WriteLine("Found {0} things to update.", results.Count);
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Fan them all out, 25 at a time
            var ss = new SemaphoreSlim(20);
            int i = 0;
            var tasks = results.Select(async (doc) =>
            {
                await ss.WaitAsync();

                string oldId = doc.id;
                doc.id = doc.Content.event_ts;
                await ExecuteWithIndefiniteRetries(oldId, async () =>
                {
                    await dc.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN), doc, new RequestOptions { PartitionKey = new PartitionKey(doc.TypeSubtype) });
                    await dc.DeleteDocumentAsync(UriFactory.CreateDocumentUri(C.CDB.DN, C.CDB.CN, oldId), new RequestOptions { PartitionKey = new PartitionKey(doc.TypeSubtype) });
                });
                Console.WriteLine("{0}: {1}->{2}", Interlocked.Increment(ref i), oldId, doc.id);

                ss.Release();
            }).ToList();
            await Task.WhenAll(tasks);
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
