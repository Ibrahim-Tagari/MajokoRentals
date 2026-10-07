using FirebaseAdmin.Auth;
using Microsoft.Extensions.Logging;

namespace MajokoRentals.Core.Services
{
    public class FirebaseMvcAuthService : IFirebaseMvcAuthService
    {
        private readonly IFirebaseAuthService _firebaseAuth;
        private readonly IUserProfileService _userProfile;
        private readonly ILogger<FirebaseMvcAuthService> _logger;

        public FirebaseMvcAuthService(
            IFirebaseAuthService firebaseAuth,
            IUserProfileService userProfile,
            ILogger<FirebaseMvcAuthService> logger)
        {
            _firebaseAuth = firebaseAuth;
            _userProfile = userProfile;
            _logger = logger;
        }

        public async Task<FirebaseToken> VerifyIdTokenAsync(string idToken)
        {
            try
            {
                return await _firebaseAuth.VerifyIdTokenAsync(idToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying Firebase ID token");
                throw;
            }
        }

        public async Task<(UserRecord firebaseUser, UserProfile profile)> RegisterUserAsync(
            string email, string password, string firstName, string lastName, string role = "Applicant")
        {
            try
            {
                // Create Firebase Authentication user
                var displayName = $"{firstName} {lastName}";
                var firebaseUser = await _firebaseAuth.CreateUserAsync(email, password, displayName);

                // Create user profile in Firestore
                var profile = new UserProfile
                {
                    Uid = firebaseUser.Uid,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    Role = role,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _userProfile.CreateUserProfileAsync(firebaseUser.Uid, profile);

                // Set custom claims for role-based access
                var claims = new Dictionary<string, object>
                {
                    { "role", role }
                };
                await _firebaseAuth.SetCustomUserClaimsAsync(firebaseUser.Uid, claims);

                _logger.LogInformation($"User registered successfully: {email} with role: {role}");

                return (firebaseUser, profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error registering user: {email}");
                throw;
            }
        }

        public async Task<UserProfile?> GetUserProfileAsync(string uid)
        {
            try
            {
                return await _userProfile.GetUserProfileAsync(uid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user profile: {uid}");
                return null;
            }
        }

        public async Task UpdateUserRoleAsync(string uid, string role)
        {
            try
            {
                // Update custom claims in Firebase Auth
                var claims = new Dictionary<string, object>
                {
                    { "role", role }
                };
                await _firebaseAuth.SetCustomUserClaimsAsync(uid, claims);

                // Update role in Firestore profile
                var profile = await _userProfile.GetUserProfileAsync(uid);
                if (profile != null)
                {
                    profile.Role = role;
                    await _userProfile.UpdateUserProfileAsync(uid, profile);
                }

                _logger.LogInformation($"User role updated: {uid} -> {role}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user role: {uid}");
                throw;
            }
        }

        public async Task<bool> UserHasRoleAsync(string uid, string role)
        {
            try
            {
                return await _userProfile.UserHasRoleAsync(uid, role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking user role: {uid}");
                return false;
            }
        }

        public async Task<string> GetUserRoleAsync(string uid)
        {
            try
            {
                var profile = await _userProfile.GetUserProfileAsync(uid);
                return profile?.Role ?? "Applicant";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user role: {uid}");
                return "Applicant";
            }
        }
    }
}
