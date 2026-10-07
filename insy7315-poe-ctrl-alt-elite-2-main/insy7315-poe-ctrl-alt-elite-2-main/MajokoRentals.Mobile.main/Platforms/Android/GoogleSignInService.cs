using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common.Apis;
using Android.Gms.Extensions;
using Microsoft.Maui.ApplicationModel;

namespace MajokoRentals.Mobile.main.Platforms.Android
{
    public class GoogleSignInService
    {
        private const int RC_SIGN_IN = 9001;
        private static GoogleSignInClient? _googleSignInClient;
        private static TaskCompletionSource<GoogleSignInAccount?>? _signInTcs;

        public static void Initialize(string webClientId)
        {
            System.Diagnostics.Debug.WriteLine($"[GoogleSignIn] Initializing with Web Client ID: {webClientId}");
            System.Diagnostics.Debug.WriteLine($"[GoogleSignIn] Package: com.companyname.majokorentals.mobile");

            var gso = new GoogleSignInOptions.Builder(GoogleSignInOptions.DefaultSignIn)
                .RequestIdToken(webClientId)
                .RequestEmail()
                .Build();

            var context = Platform.CurrentActivity ?? throw new InvalidOperationException("No current activity");
            _googleSignInClient = GoogleSignIn.GetClient(context, gso);

            System.Diagnostics.Debug.WriteLine("[GoogleSignIn] Client initialized successfully");
        }

        public static async Task<GoogleSignInAccount?> SignInAsync()
        {
            if (_googleSignInClient == null)
                throw new InvalidOperationException("GoogleSignInService not initialized. Call Initialize first.");

            var context = Platform.CurrentActivity ?? throw new InvalidOperationException("No current activity");

            _signInTcs = new TaskCompletionSource<GoogleSignInAccount?>();

            var signInIntent = _googleSignInClient.SignInIntent;
            context.StartActivityForResult(signInIntent, RC_SIGN_IN);

            return await _signInTcs.Task;
        }

        public static void HandleActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            if (requestCode == RC_SIGN_IN)
            {
                var task = GoogleSignIn.GetSignedInAccountFromIntent(data);
                try
                {
                    var account = (GoogleSignInAccount?)task.GetResult(Java.Lang.Class.FromType(typeof(ApiException)));
                    _signInTcs?.TrySetResult(account);
                }
                catch (ApiException e)
                {
                    // Provide detailed error information for troubleshooting
                    var errorMessage = $"Google sign-in failed with error code {e.StatusCode}";

                    // Common error codes:
                    // 10 = DEVELOPER_ERROR (OAuth client config issue)
                    // 12501 = USER_CANCELLED
                    // 7 = NETWORK_ERROR
                    switch (e.StatusCode)
                    {
                        case 10:
                            errorMessage += "\n\nDEVELOPER_ERROR: This usually means:\n" +
                                          "1. SHA-1 fingerprint not added to Google Cloud Console OAuth client\n" +
                                          "2. OAuth Consent Screen not configured\n" +
                                          "3. Wrong OAuth Client ID being used\n" +
                                          "4. Package name mismatch\n\n" +
                                          "Your SHA-1: F7:27:B0:F8:2C:F6:37:07:65:BA:B3:D0:EB:BC:83:67:D6:8E:60:A8\n" +
                                          "Package: com.companyname.majokorentals.mobile";
                            break;
                        case 12501:
                            errorMessage += " (User cancelled)";
                            break;
                        case 7:
                            errorMessage += " (Network error)";
                            break;
                    }

                    _signInTcs?.TrySetException(new Exception(errorMessage, e));
                }
            }
        }

        public static async Task SignOutAsync()
        {
            if (_googleSignInClient != null)
            {
                await _googleSignInClient.SignOut();
            }
        }

        public static Task<string?> GetFirebaseTokenAsync(GoogleSignInAccount account)
        {
            // Return the Google ID token directly
            // Firebase authentication will be handled by FirebaseAuthentication.net library
            return Task.FromResult(account?.IdToken);
        }
    }
}
