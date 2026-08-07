using System.Net;
using System.Text;

namespace Nestly.Catalog.Tests;

/// <summary>
/// A snapshot of one outbound request, taken inside the handler.
/// </summary>
/// <remarks>
/// A snapshot rather than the <see cref="HttpRequestMessage"/> itself because
/// well-behaved callers dispose the request (and therefore its content stream)
/// as soon as the send completes, so a test that held on to the live object
/// would find the body gone by the time it asserted on it.
/// </remarks>
public sealed record RecordedHttpRequest(
    HttpMethod Method,
    Uri? RequestUri,
    IReadOnlyDictionary<string, string> Headers,
    string Body)
{
    /// <summary>The named header's values joined with commas, or <c>null</c> when it was not sent.</summary>
    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}

/// <summary>
/// An <see cref="HttpMessageHandler"/> that answers from a delegate and records
/// what it was asked, so an outbound integration can be driven - including
/// through every one of its failure paths - with no network and no real
/// endpoint.
/// </summary>
/// <remarks>
/// The responder runs <i>after</i> the request is recorded, so a test whose
/// responder throws (transport failure, timeout, an unclassified fault) can
/// still assert on what was about to go over the wire.
/// </remarks>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<RecordedHttpRequest, HttpResponseMessage> _respond;

    public StubHttpMessageHandler(Func<RecordedHttpRequest, HttpResponseMessage> respond) => _respond = respond;

    /// <summary>Every request this handler was asked to send, in order.</summary>
    public List<RecordedHttpRequest> Requests { get; } = [];

    public static StubHttpMessageHandler Responding(
        HttpStatusCode statusCode,
        string body = "",
        string contentType = "application/json") =>
        new(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType)
        });

    public static StubHttpMessageHandler RespondingWithJson(string json) =>
        Responding(HttpStatusCode.OK, json);

    /// <summary>
    /// Fails the send. Takes a factory rather than an exception instance so a
    /// handler reused across several sends throws a fresh exception each time,
    /// the way a real transport would.
    /// </summary>
    public static StubHttpMessageHandler Throwing(Func<Exception> exception) =>
        new(_ => throw exception());

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var recorded = new RecordedHttpRequest(
            request.Method,
            request.RequestUri,
            request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase),
            body);

        Requests.Add(recorded);

        return _respond(recorded);
    }
}

/// <summary>
/// An <see cref="IHttpClientFactory"/> that hands every caller a client built
/// on one stubbed handler - the seam a named <c>HttpClient</c> registration
/// exists to provide.
/// </summary>
public sealed class StubHttpClientFactory : IHttpClientFactory, IDisposable
{
    private readonly HttpMessageHandler _handler;
    private readonly List<HttpClient> _clients = [];

    public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    /// <summary>The client names asked for, so a test can pin which registration is being used.</summary>
    public List<string> RequestedClientNames { get; } = [];

    public HttpClient CreateClient(string name)
    {
        RequestedClientNames.Add(name);

        // disposeHandler: false - the handler outlives the client here, because
        // tests assert on its recorded requests after the call under test has
        // finished with the client.
        var client = new HttpClient(_handler, disposeHandler: false);
        _clients.Add(client);
        return client;
    }

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _clients.Clear();
        _handler.Dispose();
    }
}
