function rething_count(id) {
    var collection = getContext().getCollection();

    // Get the document whose count must be increased
    var queryAcc = collection.queryDocuments(
        collection.getSelfLink(),
        "SELECT * FROM r WHERE r.id = '" + id + "'",
        {},
        function (err, docs) {
            if (err) throw err;
            if (docs.length !== 1) throw new Error('Did not receive only one document.');
            var doc = docs[0];

            // Update count
            doc.count++;

            var repAcc = collection.replaceDocument(
                doc._self,
                doc,
                function (err, docReplaced) {
                    if (err) throw err;
                    context.getResponse().setBody(JSON.stringify(docReplaced));
                });
            if (!repAcc) throw 'Could not replace document.';
        });

    if (!queryAcc) throw 'The query was not accepted by the server.';
}
