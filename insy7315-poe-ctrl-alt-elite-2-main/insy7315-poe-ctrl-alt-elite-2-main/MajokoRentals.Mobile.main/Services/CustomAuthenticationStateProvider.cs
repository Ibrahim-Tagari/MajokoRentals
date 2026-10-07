using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace MajokoRentals.Mobile.main.Services
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly IAuthenticationService _authService;

        public CustomAuthenticationStateProvider(IAuthenticationService authService)
        {
            _authService = authService;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _authService.GetTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            try
            {
                var claims = ParseClaimsFromJwt(token);
                var identity = new ClaimsIdentity(claims, "jwt");
                var user = new ClaimsPrincipal(identity);

                return new AuthenticationState(user);
            }
            catch
            {
                // Token is invalid or expired
                await _authService.LogoutAsync();
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
        }

        public void NotifyUserAuthentication(string token)
        {
            var claims = ParseClaimsFromJwt(token);
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        public void NotifyUserLogout()
        {
            var identity = new ClaimsIdentity();
            var user = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);

            var claims = new List<Claim>();

            foreach (var claim in token.Claims)
            {
                // Map JWT claim types to standard claim types
                if (claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" ||
                    claim.Type == "sub")
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, claim.Value));
                }
                else if (claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" ||
                         claim.Type == "unique_name")
                {
                    claims.Add(new Claim(ClaimTypes.Name, claim.Value));
                }
                else if (claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress" ||
                         claim.Type == "email")
                {
                    claims.Add(new Claim(ClaimTypes.Email, claim.Value));
                }
                else if (claim.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" ||
                         claim.Type == "role")
                {
                    claims.Add(new Claim(ClaimTypes.Role, claim.Value));
                }
                else
                {
                    claims.Add(claim);
                }
            }

            return claims;
        }
    }
}
