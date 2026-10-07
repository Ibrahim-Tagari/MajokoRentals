using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MajokoRentals.Mobile.main.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private const string TokenKey = "auth_token";
        private string? _cachedToken;

        public async Task<string?> GetTokenAsync()
        {
            if (!string.IsNullOrEmpty(_cachedToken))
                return _cachedToken;

            try
            {
                _cachedToken = await SecureStorage.GetAsync(TokenKey);
                return _cachedToken;
            }
            catch (Exception)
            {
                // SecureStorage might not be available on all platforms
                return null;
            }
        }

        public async Task SetTokenAsync(string token)
        {
            _cachedToken = token;

            try
            {
                await SecureStorage.SetAsync(TokenKey, token);
            }
            catch (Exception)
            {
                // SecureStorage might not be available on all platforms
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            var token = await GetTokenAsync();

            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // Check if token is expired
                return jwtToken.ValidTo > DateTime.UtcNow;
            }
            catch
            {
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            _cachedToken = null;

            try
            {
                SecureStorage.Remove(TokenKey);
            }
            catch (Exception)
            {
                // SecureStorage might not be available on all platforms
            }

            await Task.CompletedTask;
        }

        public async Task<AuthUserInfo?> GetUserInfoAsync()
        {
            var token = await GetTokenAsync();

            if (string.IsNullOrEmpty(token))
                return null;

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var userId = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value ?? "";

                var email = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.Email || c.Type == "email")?.Value ?? "";

                var name = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.Name || c.Type == "unique_name")?.Value ?? "";

                var roles = jwtToken.Claims
                    .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                    .Select(c => c.Value)
                    .ToList();

                return new AuthUserInfo
                {
                    UserId = userId,
                    Email = email,
                    Name = name,
                    Roles = roles
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
