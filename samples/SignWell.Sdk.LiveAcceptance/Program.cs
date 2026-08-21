using SignWell.Sdk;
using SignWell.Sdk.Embedded;
using SignWell.Sdk.Errors;
using SignWell.Sdk.Models;

static string Required(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"{name} is required.");

var apiKey = Required("SIGNWELL_API_KEY");
var documentId = Guid.Parse(Required("SIGNWELL_COMPLETED_DOCUMENT_ID"));
var nom151DocumentId = Guid.Parse(Required("SIGNWELL_NOM151_DOCUMENT_ID"));
var templateIds = Required("SIGNWELL_TEMPLATE_IDS").Split(',').Select(Guid.Parse).ToArray();
var fixtureFileUrl = Required("SIGNWELL_FIXTURE_FILE_URL");
var recipientEmail = Required("SIGNWELL_RECIPIENT_EMAIL");
var templatePlaceholder = Environment.GetEnvironmentVariable("SIGNWELL_TEMPLATE_PLACEHOLDER_NAME");

using var client = new SignWellClient(apiKey);
var me = await client.GetMeAsync();
Console.WriteLine($"Account lookup: {me.GetType().Name}");

await foreach (var page in client.Documents.GetPagesAsync(limit: 2))
{
    Console.WriteLine($"Document page: {page.CurrentPage}");
    break;
}
await foreach (var page in client.Templates.GetPagesAsync(limit: 2))
{
    Console.WriteLine($"Template page: {page.CurrentPage}");
    break;
}

using (var pdf = await client.Documents.GetCompletedPdfStreamAsync(documentId))
    Console.WriteLine($"Completed PDF first byte: {pdf.ReadByte()}");
var pdfUrl = await client.Documents.GetCompletedPdfUrlAsync(documentId);
Console.WriteLine($"Completed PDF URL: {new Uri(pdfUrl.FileUrl).Host}");

using (var csv = await client.BulkSends.GetCsvTemplateStreamAsync(templateIds))
    Console.WriteLine($"Bulk CSV first byte: {csv.ReadByte()}");
var csvBase64 = await client.BulkSends.GetCsvTemplateBase64Async(templateIds);
Console.WriteLine($"Bulk CSV base64 bytes: {csvBase64.Data.Length}");

try
{
    using (var certificate = await client.Regional.GetNom151StreamAsync(nom151DocumentId))
        Console.WriteLine($"NOM-151 first byte: {certificate.ReadByte()}");
    Console.WriteLine($"NOM-151 URL host: {new Uri((await client.Regional.GetNom151UrlAsync(nom151DocumentId)).FileUrl).Host}");
    Console.WriteLine($"NOM-151 object: {(await client.Regional.GetNom151ObjectAsync(nom151DocumentId)).GetType().Name}");
}
catch (ApiStatusException exception) when (exception is NotFoundException or UnprocessableEntityException)
{
    Console.WriteLine("NOM-151 live fixture unavailable; offline response-mode tests remain required.");
}

var created = new List<Guid>();
try
{
    var draft = await client.Embedded.CreateRequestingDocumentAsync(
        ".NET SDK live acceptance draft",
        [new EmbeddedFile("fixture.pdf", FileUrl: fixtureFileUrl)],
        [new EmbeddedRecipient("SDK Acceptance", recipientEmail)],
        testMode: true);
    created.Add(draft.Id);
    Console.WriteLine($"Draft created: {draft.Id}");
    Console.WriteLine("Update/send requires an interactively edited draft; offline alias coverage remains required.");

    var signing = await client.Embedded.CreateSigningDocumentAsync(
        ".NET SDK live acceptance embedded signing",
        [new EmbeddedFile("fixture.pdf", FileUrl: fixtureFileUrl)],
        [new EmbeddedRecipient("SDK Acceptance", recipientEmail)],
        withSignaturePage: true,
        testMode: true);
    created.Add(signing.Id);
    Console.WriteLine($"Embedded URL present: {EmbeddedWorkflows.SigningUrl(signing) is not null}");

    var fieldSigning = await client.Embedded.CreateSigningDocumentAsync(
        ".NET SDK live acceptance explicit fields",
        [new EmbeddedFile("fixture.pdf", FileUrl: fixtureFileUrl)],
        [new EmbeddedRecipient("SDK Acceptance", recipientEmail)],
        [[new EmbeddedField(72, 520, 1, FieldType.Signature)]],
        testMode: true);
    created.Add(fieldSigning.Id);
    Console.WriteLine($"Explicit-field document created: {fieldSigning.Id}");

    using var fixtureClient = new HttpClient();
    var fixtureBase64 = Convert.ToBase64String(await fixtureClient.GetByteArrayAsync(fixtureFileUrl));
    var base64Signing = await client.Embedded.CreateSigningDocumentAsync(
        ".NET SDK live acceptance base64 PDF",
        [new EmbeddedFile("fixture.pdf", FileBase64: fixtureBase64)],
        [new EmbeddedRecipient("SDK Acceptance", recipientEmail)],
        withSignaturePage: true,
        testMode: true);
    created.Add(base64Signing.Id);
    Console.WriteLine($"Base64 PDF document created: {base64Signing.Id}");

    var textTags = await client.Embedded.CreateSigningDocumentAsync(
        ".NET SDK live acceptance text tags",
        [new EmbeddedFile("fixture.pdf", FileUrl: fixtureFileUrl)],
        [new EmbeddedRecipient("SDK Acceptance", recipientEmail)],
        textTags: true,
        testMode: true);
    created.Add(textTags.Id);
    Console.WriteLine($"Text-tag document created: {textTags.Id}");

    var templateRecipient = new EmbeddedTemplateRecipient(
        "SDK Acceptance",
        recipientEmail,
        PlaceholderName: templatePlaceholder);
    var singleTemplate = await client.Embedded.CreateSigningDocumentFromTemplateAsync(
        new EmbeddedTemplateSelector(templateIds[0]),
        [templateRecipient],
        testMode: true);
    created.Add(singleTemplate.Id);
    Console.WriteLine($"Single-template document created: {singleTemplate.Id}");

    if (templateIds.Length > 1)
    {
        var multiTemplate = await client.Embedded.CreateSigningDocumentFromTemplateAsync(
            new EmbeddedTemplateSelector(TemplateIds: templateIds),
            [templateRecipient],
            testMode: true);
        created.Add(multiTemplate.Id);
        Console.WriteLine($"Multi-template document created: {multiTemplate.Id}");
    }

    try
    {
        await client.Documents.GetAsync(Guid.NewGuid());
        throw new InvalidOperationException("Expected a typed not-found failure.");
    }
    catch (NotFoundException)
    {
        Console.WriteLine("Typed failure mapping: NotFoundException");
    }
}
finally
{
    foreach (var id in created)
    {
        try { await client.Documents.DeleteAsync(id); }
        catch (SignWellException exception) { Console.Error.WriteLine($"Cleanup failed for {id}: {exception.GetType().Name}"); }
    }
}
