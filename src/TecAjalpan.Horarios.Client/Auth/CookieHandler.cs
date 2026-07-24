using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace TecAjalpan.Horarios.Client.Auth;

internal sealed class CookieHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(
            BrowserRequestCredentials.Include);

        return base.SendAsync(request, cancellationToken);
        //request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        //return base.SendAsync(request, cancellationToken);
    }
}
