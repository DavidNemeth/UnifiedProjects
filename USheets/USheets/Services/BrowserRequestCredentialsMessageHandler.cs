using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace USheets.Services; 

public class BrowserRequestCredentialsMessageHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Tell the browser to include credentials (cookies) for this request.
        // This is necessary for cross-origin authenticated API calls.
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        return base.SendAsync(request, cancellationToken);
    }
}