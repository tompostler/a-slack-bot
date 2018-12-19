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

namespace upgrade._181218
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var dc = new DocumentClient(new Uri("https://aslackbot.documents.azure.com:443/"), args[0]);

            Task memoryMonitor = Task.Run(async () =>
            {
                while (true)
                {
                    Console.WriteLine("MB in use: {0:#,#.0}", System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1000d / 1000d);
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            });

            // Increase throughputs for the migration
            await ChangeDocumentCollectionThroughput(dc, C.CDB.DCUri, 4000);
            await ChangeDatabaseThroughput(dc, UriFactory.CreateDatabaseUri(C.CDB2.DN), 4000);

            try
            {
                await Task.WhenAll(new[]
                {
                    MigrateGeneric<a_slack_bot.Documents.Event>(dc, C.CDB2.Col.SlackEvents),
                    MigrateGeneric<a_slack_bot.Documents.Blackjack>(dc, C.CDB2.Col.GamesBlackjack, "SELECT * FROM b WHERE b.id != 'BlackjackStandings'"),
                    MigrateStandings(dc),
                    MigrateGeneric<a_slack_bot.Documents.OAuthUserToken>(dc, C.CDB2.Col.SlackOAuthTokens),
                    MigrateGeneric<a_slack_bot.Documents.Response>(dc, C.CDB2.Col.CustomResponses)
                });
            }
            finally
            {
                // And put them back to their mins
                await ChangeDocumentCollectionThroughput(dc, C.CDB.DCUri, 400);
                await ChangeDatabaseThroughput(dc, UriFactory.CreateDatabaseUri(C.CDB2.DN), 600);
            }

            Console.WriteLine();
            Console.WriteLine("Done.");
        }

        // Only do 1000 things at a time
        private static readonly SemaphoreSlim ss = new SemaphoreSlim(1000);

        private static async Task MigrateStandings(DocumentClient dc)
        {
            var standings = await dc.ReadDocumentAsync<a_slack_bot.Documents.Standings>(
                UriFactory.CreateDocumentUri(C.CDB2.DN, C.CDB2.Col.GamesBlackjack, "BlackjackStandings"),
                new RequestOptions { PartitionKey = new PartitionKey("BlackjackStandings") });

            await ExecuteWithIndefiniteRetries(standings.Document.Id, () =>
            {
                return dc.UpsertDocumentAsync(
                    C.CDB.DCUri,
                    standings.Document,
                    new RequestOptions { PartitionKey = standings.Document.PK },
                    disableAutomaticIdGeneration: true);
            });
            Console.WriteLine("Migrated standings.");

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        private static async Task MigrateGeneric<T>(DocumentClient dc, string docColName, string queryOverride = null)
            where T : a_slack_bot.Documents.Base
        {
            IDocumentQuery<T> query = null;
            if (queryOverride == null)
                query = dc.CreateDocumentQuery<T>(
                    C.CDB2.CUs[docColName],
                    new FeedOptions { EnableCrossPartitionQuery = true })
                    .AsDocumentQuery();
            else
                query = dc.CreateDocumentQuery<T>(
                    C.CDB2.CUs[docColName],
                    queryOverride,
                    new FeedOptions { EnableCrossPartitionQuery = true })
                    .AsDocumentQuery();

            // Just load it all in memory. Should be less than a few hundred MBs
            List<T> results = new List<T>();
            while (query.HasMoreResults)
            {
                results.AddRange(await query.ExecuteNextAsync<T>());
                Console.WriteLine("Found {0} {1} to migrate...", results.Count, docColName);
            }
            Console.WriteLine("Found {0} {1} to migrate.", results.Count, docColName);
            await Task.Delay(TimeSpan.FromSeconds(2));

            int i = 0;
            var tasks = results.Select(async (doc) =>
            {
                await ss.WaitAsync();

                await ExecuteWithIndefiniteRetries(doc.Id as string, async () =>
                {
                    await dc.UpsertDocumentAsync(C.CDB.DCUri, doc, new RequestOptions { PartitionKey = doc.PK, ConsistencyLevel = ConsistencyLevel.Eventual }, disableAutomaticIdGeneration: true);
                });
                Console.WriteLine("{0}: {1} {2}", Interlocked.Increment(ref i), docColName, doc.Id);

                ss.Release();
            }).ToList();
            await Task.WhenAll(tasks);

            results = null;
            GC.Collect();

            await Task.Delay(TimeSpan.FromSeconds(2));
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

        private static async Task ChangeDocumentCollectionThroughput(DocumentClient dc, Uri docColUri, int offerThroughput)
        {
            Console.WriteLine();
            Console.WriteLine($"Updating offer for {docColUri} to {offerThroughput}...");
            var col = await dc.ReadDocumentCollectionAsync(docColUri);
            Console.WriteLine($"{nameof(dc.ReadDocumentCollectionAsync)}: {col.StatusCode}");
            var off = (OfferV2)dc.CreateOfferQuery().Where(o => o.ResourceLink == col.Resource.SelfLink).AsEnumerable().Single();
            Console.WriteLine($"Current offer: {off.Content.OfferThroughput}");
            off = new OfferV2(off, offerThroughput);
            var upoff = await dc.ReplaceOfferAsync(off);
            Console.WriteLine($"{nameof(dc.ReplaceOfferAsync)}: {upoff.StatusCode}");
            Console.WriteLine($"Updated offer for {docColUri} to {((OfferV2)upoff.Resource).Content.OfferThroughput}.");
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        private static async Task ChangeDatabaseThroughput(DocumentClient dc, Uri dbUri, int offerThroughput)
        {
            Console.WriteLine();
            Console.WriteLine($"Updating offer for {dbUri} to {offerThroughput}...");
            var db = await dc.ReadDatabaseAsync(dbUri);
            Console.WriteLine($"{nameof(dc.ReadDatabaseAsync)}: {db.StatusCode}");
            var off = (OfferV2)dc.CreateOfferQuery().Where(o => o.ResourceLink == db.Resource.SelfLink).AsEnumerable().Single();
            Console.WriteLine($"Current offer: {off.Content.OfferThroughput}");
            off = new OfferV2(off, offerThroughput);
            var upoff = await dc.ReplaceOfferAsync(off);
            Console.WriteLine($"{nameof(dc.ReplaceOfferAsync)}: {upoff.StatusCode}");
            Console.WriteLine($"Updated offer for {dbUri} to {((OfferV2)upoff.Resource).Content.OfferThroughput}.");
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}
