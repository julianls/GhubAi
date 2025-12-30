using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace GhubAiHub.Services;

public class ProviderTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public ProviderTokenAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Accept token from query string (SignalR client AccessTokenProvider will send it as access_token query param)
        var token = Request.Query["access_token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // For demo purposes accept any non-empty token. Replace with your validation logic.
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, token), new Claim("provider", "true") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
