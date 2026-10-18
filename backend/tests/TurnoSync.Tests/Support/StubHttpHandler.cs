using System.Collections.Concurrent;

namespace TurnoSync.Tests.Support;

/// <summary>HttpMessageHandler falso: responde com o que o teste definir e registra as chamadas.</summary>
public sealed class StubHttpHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private readonly ConcurrentQueue<Uri> _requests = new();

    public int CallCount => _requests.Count;

    public IReadOnlyCollection<Uri> Requests => _requests;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(request.RequestUri!);
        return respond(request, cancellationToken);
    }
}
