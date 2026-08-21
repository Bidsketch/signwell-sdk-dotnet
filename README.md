# SignWell .NET SDK

The official, nullable, async-first .NET SDK for the [SignWell API](https://developers.signwell.com/).

## Installation

Install the prerelease package with the .NET CLI:

```bash
dotnet add package SignWell.Sdk --prerelease
```

Or add a package reference directly:

```xml
<PackageReference Include="SignWell.Sdk" Version="0.1.0-beta.1" />
```

The package targets `netstandard2.0` and `net10.0`. The `netstandard2.0`
asset supports older consumers, including .NET Framework 4.7.2 and newer.

## Quick Start

```csharp
using SignWell.Sdk;
using SignWell.Sdk.Embedded;

var apiKey = Environment.GetEnvironmentVariable("SIGNWELL_API_KEY")
    ?? throw new InvalidOperationException("SIGNWELL_API_KEY must be set.");

using var signWell = new SignWellClient(apiKey);

var document = await signWell.Embedded.CreateSigningDocumentAsync(
    "NDA",
    [new EmbeddedFile("nda.pdf", FileUrl: "https://example.com/nda.pdf")],
    [new EmbeddedRecipient("Jane Doe", "jane@example.com")],
    withSignaturePage: true,
    testMode: true);

Console.WriteLine(document.Id);
```

The SDK sends the API key in the `X-Api-Key` header and uses
`https://www.signwell.com` by default. Test-mode documents are not legally
binding and do not count toward API billing. SMS recipient delivery is not
included; use email delivery and passcode authentication.

## Dependency Injection

Register one shared client in applications using Microsoft.Extensions.DependencyInjection:

```csharp
using SignWell.Sdk;

builder.Services.AddSignWell(options =>
{
    options.ApiKey = builder.Configuration["SignWell:ApiKey"]!;
});
```

Resolve `ISignWellClient` from the service provider. Use
`SignWellClientOptions.ConfigureHttpClientBuilder` to add application-specific
resilience policies. Automatic retries are disabled because retry safety
depends on endpoint idempotency.

## Resources, Models, And Errors

The primary namespaces are:

- `SignWell.Sdk` — client facade and dependency-injection registration
- `SignWell.Sdk.Models` — request and response models
- `SignWell.Sdk.Resources` — document, template, bulk-send, regional, account, API-application, and webhook resources
- `SignWell.Sdk.Errors` — typed status, connection, timeout, and validation exceptions
- `SignWell.Sdk.Embedded` — embedded signing and requesting helpers
- `SignWell.Sdk.Webhooks` — webhook signature and replay verification
- `SignWell.Sdk.Raw` — generated response-level clients

```csharp
using SignWell.Sdk.Errors;

try
{
    var document = await signWell.Documents.GetAsync(documentId);
}
catch (NotFoundException exception)
{
    Console.WriteLine(exception.StatusCode);
}
catch (RateLimitException exception)
{
    Console.WriteLine(exception.RateLimit.RetryAfter);
}
catch (SignWellException exception)
{
    Console.WriteLine(exception.Message);
}
```

HTTP 403 responses raise `PermissionDeniedException`, which remains compatible
with `ForbiddenException` catches. Connection and timeout failures raise
`ApiConnectionException` and `ApiTimeoutException`; both remain compatible with
`TransportException` catches.

Each resource exposes its generated `Raw` client when you need response status
codes or headers:

```csharp
var response = await signWell.Documents.Raw.GetDocumentAsync(documentId);
Console.WriteLine(response.StatusCode);
Console.WriteLine(response.Headers);
```

## Documents And Templates

Document and template list helpers support SignWell query syntax, clamp limits
to the API maximum of 50, and stream pages or individual items asynchronously:

```csharp
await foreach (var document in signWell.Documents.GetItemsAsync("status:Completed"))
{
    Console.WriteLine(document.Id);
}

await foreach (var template in signWell.Templates.GetItemsAsync("name:NDA"))
{
    Console.WriteLine(template.Id);
}
```

Wait for a document to reach one of the standard terminal statuses:

```csharp
var completed = await signWell.Documents.WaitForCompletionAsync(documentId);
```

`UpdateDocumentAsync` is an additive alias for the API's Update and Send
operation:

```csharp
using SignWell.Sdk.Models;

var sent = await signWell.UpdateDocumentAsync(
    draftDocumentId,
    new UpdateDocumentAndSendRequest(subject: "Please sign"));
```

## Convenience Workflows

`SignWellClient` provides thin wrappers for common workflows while each
resource keeps the complete generated API available through its `Raw` property:

```csharp
var account = await signWell.GetMeAsync();
var document = await signWell.CreateDocumentAsync(documentRequest);
var templateDocument = await signWell.CreateDocumentFromTemplateAsync(templateRequest);
var sent = await signWell.SendDocumentAsync(draftDocumentId, sendRequest);
await signWell.SendReminderAsync(documentId, reminderRequest);
var template = await signWell.CreateTemplateAsync(createTemplateRequest);
var updatedTemplate = await signWell.UpdateTemplateAsync(templateId, updateTemplateRequest);
var bulkSend = await signWell.CreateBulkSendAsync(bulkSendRequest);
var csvCheck = await signWell.ValidateBulkSendCsvAsync(csvRequest);
```

Recipient passcode authentication can be updated through the document resource:

```csharp
await signWell.Documents.UpdateAuthenticationAsync(documentId, authenticationRequest);
```

## Binary And JSON Response Modes

Endpoints that can return either a file or JSON have explicit methods, so
return types are never ambiguous:

```csharp
using (var pdf = await signWell.Documents.GetCompletedPdfStreamAsync(documentId))
{
    await pdf.CopyToAsync(destination);
}

var pdfUrl = await signWell.Documents.GetCompletedPdfUrlAsync(documentId);

using (var csv = await signWell.BulkSends.GetCsvTemplateStreamAsync(templateIds))
{
    await csv.CopyToAsync(destination);
}

var csvBase64 = await signWell.BulkSends.GetCsvTemplateBase64Async(templateIds);

using var certificate = await signWell.Regional.GetNom151StreamAsync(documentId);
var certificateUrl = await signWell.Regional.GetNom151UrlAsync(documentId);
var certificateObject = await signWell.Regional.GetNom151ObjectAsync(documentId);
```

Downloaded streams are not buffered in memory. Dispose them after use so the
underlying HTTP response is also disposed.

## Embedded Helpers

Create an embedded signing request with explicit fields:

```csharp
using SignWell.Sdk.Embedded;
using SignWell.Sdk.Models;

var document = await signWell.Embedded.CreateSigningDocumentAsync(
    "NDA",
    [new EmbeddedFile("nda.pdf", FileUrl: "https://example.com/nda.pdf")],
    [new EmbeddedRecipient("Jane Doe", "jane@example.com")],
    [[new EmbeddedField(20, 60, 1, FieldType.Signature)]],
    testMode: true);

var signingUrl = EmbeddedWorkflows.SigningUrl(document)
    ?? throw new InvalidOperationException("The response did not include a signing URL.");

var nonce = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
var loader = EmbeddedRenderer.ScriptTag(nonce);
var iframe = EmbeddedRenderer.SigningIframe(new EmbeddedIframeOptions(
    signingUrl,
    Events: new Dictionary<string, string>
    {
        ["completed"] = "SignWellHandlers.completed"
    },
    Nonce: nonce));
```

Embedded signing documents must provide fields for every recipient, enable a
signature page, or use text-tag mode. URL renderers accept only HTTPS SignWell
hosts by default. Custom hosts require an exact allowlist, and callback handlers
must be safe dotted JavaScript identifier paths.

The same helper supports embedded requesting, single-template signing, and
multi-template signing through `CreateRequestingDocumentAsync` and
`CreateSigningDocumentFromTemplateAsync`.

## Webhook Verification

```csharp
using System.Text.Json;
using SignWell.Sdk.Webhooks;

using var payload = JsonDocument.Parse(requestBody);
var eventData = payload.RootElement.GetProperty("event");
var webhookId = Environment.GetEnvironmentVariable("SIGNWELL_WEBHOOK_ID")
    ?? throw new InvalidOperationException("SIGNWELL_WEBHOOK_ID must be set.");

WebhookVerifier.VerifyOrThrow(
    eventData,
    webhookId,
    freshness: TimeSpan.FromMinutes(5));

var replayStore = new InMemoryWebhookReplayStore();
await WebhookVerifier.VerifyOnceOrThrowAsync(
    eventData,
    webhookId,
    replayStore,
    freshness: TimeSpan.FromMinutes(5));
```

Use replay-aware verification whenever webhook processing has side effects.
The in-memory replay store is intended for local development and single-process
applications. Multi-instance production applications must provide a shared,
atomic Redis or database implementation of `IWebhookReplayStore`.

## Examples

The repository includes a package-consumption smoke project and a manual,
test-mode live acceptance project under [`samples/`](samples/). The live
acceptance project requires controlled fixture IDs and always attempts to
delete documents it creates.

## Documentation

Generated API and model references are available in [`docs/`](docs/).

## Build

```bash
dotnet restore SignWell.Sdk.sln --locked-mode
dotnet format SignWell.Sdk.sln --verify-no-changes --no-restore
dotnet build SignWell.Sdk.sln --configuration Release --no-restore
dotnet test SignWell.Sdk.sln --configuration Release --no-build
dotnet pack src/SignWell.Sdk/SignWell.Sdk.csproj --configuration Release --no-build
```

## License

This SDK is distributed under the [MIT License](LICENSE).
