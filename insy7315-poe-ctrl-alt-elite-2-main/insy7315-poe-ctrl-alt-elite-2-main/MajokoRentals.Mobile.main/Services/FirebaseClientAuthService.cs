using Firebase.Auth;
using Firebase.Auth.Providers;
using Microsoft.Extensions.Configuration;

namespace MajokoRentals.Mobile.main.Services
{
    public class FirebaseClientAuthService : IFirebaseClientAuthService
    {
        private readonly FirebaseAuthClient _authClient;
        private readonly string _apiKey;
        private readonly string _authDomain;

        public FirebaseClientAuthService(IConfiguration configuration)
        {
            _apiKey = configuration["Firebase:ApiKey"] ?? throw new InvalidOperationException("Firebase API Key not configured");
            _authDomain = configuration["Firebase:AuthDomain"] ?? "majokorentals.firebaseapp.com";

            var config = new FirebaseAuthConfig
            {
                ApiKey = _apiKey,
                AuthDomain = _authDomain,
                Providers = new FirebaseAuthProvider[]
                {
                    new EmailProvider()
                    // Google OAuth handled manually via WebAuthenticator
                }
            };

            _authClient = new FirebaseAuthClient(config);
        }

        public async Task<string> SignInWithEmailAndPasswordAsync(string email, string password)
        {
            var userCredential = await _authClient.SignInWithEmailAndPasswordAsync(email, password);

            if (userCredential?.User == null)
            {
                throw new InvalidOperationException("Sign in failed - no user credential returned");
            }

            // Get the Firebase ID token
            var idToken = await userCredential.User.GetIdTokenAsync();
            return idToken;
        }

        public async Task<string> CreateUserWithEmailAndPasswordAsync(string email, string password)
        {
            var userCredential = await _authClient.CreateUserWithEmailAndPasswordAsync(email, password);

            if (userCredential?.User == null)
            {
                throw new InvalidOperationException("User creation failed - no user credential returned");
            }

            // Get the Firebase ID token
            var idToken = await userCredential.User.GetIdTokenAsync();
            return idToken;
        }

        public async Task SignOutAsync()
        {
            _authClient.SignOut();
            await Task.CompletedTask;
        }

        public async Task<string?> GetCurrentUserIdTokenAsync()
        {
            var user = _authClient.User;
            if (user == null)
            {
                return null;
            }

            return await user.GetIdTokenAsync();
        }

        public async Task<(string idToken, string email, string displayName, bool isNewUser)> SignInWithGoogleAsync()
        {
            // Use MAUI WebAuthenticator for Google OAuth - simplest and most reliable approach
            // Opens browser for Google sign-in, no native SDK needed!
            try
            {
                // Google OAuth endpoints - Use ANDROID client with reverse client ID (the ONLY format that works)
                var clientId = "718418939895-iv17gsgt84k9mn0sg7lohfi20iru6ns7.apps.googleusercontent.com"; // Android OAuth client
                var redirectUri = "com.googleusercontent.apps.718418939895-iv17gsgt84k9mn0sg7lohfi20iru6ns7:/oauth2redirect"; // Reverse client ID format (auto-accepted by Google)

                var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                    $"client_id={clientId}&" +
                    $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                    $"response_type=code&" +
                    $"scope={Uri.EscapeDataString("openid profile email")}&" +
                    $"prompt=select_account&" +  // Force account selection every time
                    $"access_type=offline";

                // Open browser for Google sign-in
                var authResult = await WebAuthenticator.AuthenticateAsync(
                    new Uri(authUrl),
                    new Uri(redirectUri));

                // Check for errors in the OAuth response
                if (authResult.Properties.TryGetValue("error", out var error))
                {
                    var errorDescription = authResult.Properties.TryGetValue("error_description", out var desc) ? desc : "Unknown error";
                    throw new InvalidOperationException($"Google OAuth error: {error} - {errorDescription}");
                }

                // Get authorization code
                if (!authResult.Properties.TryGetValue("code", out var authCode))
                {
                    // Log what we actually received for debugging
                    var receivedKeys = string.Join(", ", authResult.Properties.Keys);
                    throw new InvalidOperationException($"Authorization code not found in response. Received keys: {receivedKeys}");
                }

                // Exchange authorization code for Google ID token
                using var httpClient = new HttpClient();
                // Web OAuth clients don't require client_secret for PKCE flow or mobile apps
                var tokenRequest = new Dictionary<string, string>
                {
                    { "code", authCode },
                    { "client_id", clientId },
                    { "redirect_uri", redirectUri },
                    { "grant_type", "authorization_code" }
                };

                HttpResponseMessage tokenResponse;
                try
                {
                    tokenResponse = await httpClient.PostAsync(
                        "https://oauth2.googleapis.com/token",
                        new FormUrlEncodedContent(tokenRequest));
                }
                catch (HttpRequestException httpEx)
                {
                    throw new InvalidOperationException($"Network error connecting to Google OAuth: {httpEx.Message}. Check internet connection and network security config.", httpEx);
                }
                catch (TaskCanceledException timeoutEx)
                {
                    throw new InvalidOperationException($"Request to Google OAuth timed out: {timeoutEx.Message}", timeoutEx);
                }

                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                if (!tokenResponse.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Failed to get Google token (HTTP {tokenResponse.StatusCode}): {tokenJson}");

                var tokenData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(tokenJson);
                var googleIdToken = tokenData.GetProperty("id_token").GetString();

                if (string.IsNullOrEmpty(googleIdToken))
                    throw new InvalidOperationException("Failed to get Google ID token");

                // Exchange Google ID token for Firebase token
                var firebaseRequest = new
                {
                    postBody = $"id_token={googleIdToken}&providerId=google.com",
                    requestUri = "http://localhost",
                    returnIdpCredential = true,
                    returnSecureToken = true
                };

                var firebaseJson = System.Text.Json.JsonSerializer.Serialize(firebaseRequest);
                var firebaseContent = new StringContent(firebaseJson, System.Text.Encoding.UTF8, "application/json");
                HttpResponseMessage firebaseResponse;
                try
                {
                    firebaseResponse = await httpClient.PostAsync(
                        $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={_apiKey}",
                        firebaseContent);
                }
                catch (HttpRequestException httpEx)
                {
                    throw new InvalidOperationException($"Network error connecting to Firebase: {httpEx.Message}", httpEx);
                }
                catch (TaskCanceledException timeoutEx)
                {
                    throw new InvalidOperationException($"Request to Firebase timed out: {timeoutEx.Message}", timeoutEx);
                }

                var firebaseResponseJson = await firebaseResponse.Content.ReadAsStringAsync();
                if (!firebaseResponse.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Failed to authenticate with Firebase (HTTP {firebaseResponse.StatusCode}): {firebaseResponseJson}");

                var firebaseData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(firebaseResponseJson);
                var firebaseToken = firebaseData.GetProperty("idToken").GetString();
                var isNewUser = firebaseData.GetProperty("isNewUser").GetBoolean();

                if (string.IsNullOrEmpty(firebaseToken))
                    throw new InvalidOperationException("Failed to get Firebase token");

                // Parse Firebase token for user info
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(firebaseToken);
                var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? "";
                var displayName = jwtToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? "";

                return (firebaseToken, email, displayName, isNewUser);
            }
            catch (TaskCanceledException)
            {
                throw new InvalidOperationException("Google sign-in was cancelled by the user");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Google sign-in failed: {ex.Message}", ex);
            }
        }

        public Task<string?> GetCurrentUserEmailAsync()
        {
            var user = _authClient.User;
            return Task.FromResult(user?.Info.Email);
        }

        public Task<string?> GetCurrentUserDisplayNameAsync()
        {
            var user = _authClient.User;
            return Task.FromResult(user?.Info.DisplayName);
        }
    }
}
