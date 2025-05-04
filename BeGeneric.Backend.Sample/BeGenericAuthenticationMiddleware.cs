using System.Security.Claims;

namespace BeGeneric.Backend.Sample
{
    public class BeGenericAuthenticationMiddleware
    {
        private readonly RequestDelegate next;

        public BeGenericAuthenticationMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext.User != null && (httpContext.User.Identity?.IsAuthenticated ?? false))
            {
                string id = httpContext.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;

                var identity = httpContext.User.Identities.First();

                identity.AddClaim(new Claim("provider", httpContext.User.Identity.AuthenticationType ?? string.Empty));
            }
            else
            {
                var identity = new ClaimsIdentity(new Claim[] { new("id", $"(unknown user)-{httpContext.Connection.RemoteIpAddress}"), new(ClaimTypes.Role, "anonymous") }, "Basic");
                httpContext.User = new ClaimsPrincipal(identity);
            }

            await next(httpContext);
        }
    }
}
