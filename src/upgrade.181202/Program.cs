using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace upgrade._181202
{
    /// <summary>
    /// Migrate old data format to the new format (run only once!)
    /// </summary>
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var dc = new DocumentClient(new Uri("https://aslackbot.documents.azure.com:443/"), args[0]);

            //
            // CustomResponses
            //
            var query = dc.CreateDocumentQuery(
                UriFactory.CreateDocumentCollectionUri(C.CDB.DN, C.CDB.CN),
                "SELECT * FROM c WHERE c.Type = 'Response' AND c.id <> 'ResponsesUsed'",
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

            // Fan them all out, 20 at a time
            var ss = new SemaphoreSlim(20);
            int i = 0;
            var tasks = results.Select(async (doc) =>
            {
                await ss.WaitAsync();

                await dc.UpsertDocumentAsync(
                    UriFactory.CreateDocumentCollectionUri(C.CDB2.DN, C.CDB2.Col.CustomResponses),
                    new
                    {
                        id = doc.id,
                        key = doc.Subtype,
                        value = doc.Content,
                        user_id = doc.user_id,
                        count = 0,
                        random = Guid.NewGuid()
                    },
                    new RequestOptions { PartitionKey = new PartitionKey(doc.Subtype) },
                    disableAutomaticIdGeneration: true);
                Console.WriteLine("{0}: {1}", Interlocked.Increment(ref i), doc.id);

                ss.Release();
            }).ToList();
            await Task.WhenAll(tasks);
        }
    }
}
