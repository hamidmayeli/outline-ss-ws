using System.Net;

namespace OutlineManager.API.Tests.Mocks;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = new();

    public void AddRoute(string query, string responseJson)
    {
        _routes[query] = _ => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseJson)
        };
    }

    public void AddRoute(string query, Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _routes[query] = handler;
    }

    public void ClearRoutes()
    {
        _routes.Clear();
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        if (request.RequestUri == null)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        }

        var path = request.RequestUri.AbsolutePath;
        var query = Uri.UnescapeDataString(request.RequestUri.Query);
        
        // Sort routes by key length (descending) to match most specific routes first
        // Then check in reverse order of insertion for same-length keys
        var orderedRoutes = _routes
            .OrderByDescending(r => r.Key.Length)
            .ThenByDescending(r => _routes.Keys.ToList().IndexOf(r.Key));
        
        foreach (var route in orderedRoutes)
        {
            // Match against path or query
            // For path matching, normalize the route key to handle both "/metrics" and "metrics"
            var normalizedRouteKey = route.Key.TrimStart('/');
            var normalizedPath = path.TrimStart('/');
            
            if (normalizedPath.Contains(normalizedRouteKey, StringComparison.OrdinalIgnoreCase) ||
                query.Contains(route.Key, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(route.Value(request));
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
