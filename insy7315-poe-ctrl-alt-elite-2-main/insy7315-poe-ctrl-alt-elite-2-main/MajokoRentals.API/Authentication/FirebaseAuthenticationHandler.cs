using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MajokoRentals.Core.Services;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace MajokoRentals.API.Authentication
{
    public class FirebaseAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IFirebaseAuthService _firebaseAuth;

        public FirebaseAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IFirebaseAuthService firebaseAuth)
            : base(options, logger, encoder)
        {
            _firebaseAuth = firebaseAuth;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Check if Authorization header exists
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                return AuthenticateResult.Fail("Missing Authorization header");
            }

            string? authorizationHeader = Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authorizationHeader))
            {
                return AuthenticateResult.Fail("Missing Authorization header");
            }

            // Extract Bearer token
            if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.Fail("Invalid Authorization header format");
            }

            string token = authorizationHeader.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrEmpty(token))
            {
                return AuthenticateResult.Fail("Missing token");
            }

            try
            {
                // Verify Firebase ID token
                FirebaseToken firebaseToken = await _firebaseAuth.VerifyIdTokenAsync(token);

                // Create claims from Firebase token
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, firebaseToken.Uid),
                    new Claim(ClaimTypes.Email, firebaseToken.Claims.ContainsKey("email") ? firebaseToken.Claims["email"].ToString()! : ""),
                    new Claim("firebase_uid", firebaseToken.Uid)
                };

                // Add role claim if exists
                if (firebaseToken.Claims.ContainsKey("role"))
                {
                    claims.Add(new Claim(ClaimTypes.Role, firebaseToken.Claims["role"].ToString()!));
                }

                // Add custom claims
                foreach (var claim in firebaseToken.Claims)
                {
                    if (claim.Key != "email" && claim.Key != "role")
                    {
                        claims.Add(new Claim(claim.Key, claim.Value.ToString()!));
                    }
                }

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return AuthenticateResult.Success(ticket);
            }
            catch (FirebaseAuthException ex)
            {
                Logger.LogWarning(ex, "Firebase authentication failed");
                return AuthenticateResult.Fail($"Firebase authentication failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error during authentication");
                return AuthenticateResult.Fail($"Authentication error: {ex.Message}");
            }
        }
    }
}
