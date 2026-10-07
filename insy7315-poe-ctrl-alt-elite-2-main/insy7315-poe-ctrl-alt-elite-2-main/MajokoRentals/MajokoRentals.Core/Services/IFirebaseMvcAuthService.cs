using FirebaseAdmin.Auth;

namespace MajokoRentals.Core.Services
{
    public interface IFirebaseMvcAuthService
    {
        /// <summary>
        /// Verify Firebase ID token and return user claims
        /// </summary>
        Task<FirebaseToken> VerifyIdTokenAsync(string idToken);

        /// <summary>
        /// Create a user with email and password, and create profile in Firestore
        /// </summary>
        Task<(UserRecord firebaseUser, UserProfile profile)> RegisterUserAsync(string email, string password, string firstName, string lastName, string role = "Applicant");

        /// <summary>
        /// Get user profile from Firestore by Firebase UID
        /// </summary>
        Task<UserProfile?> GetUserProfileAsync(string uid);

        /// <summary>
        /// Update user role (sets both custom claims and Firestore profile)
        /// </summary>
        Task UpdateUserRoleAsync(string uid, string role);

        /// <summary>
        /// Check if user has a specific role
        /// </summary>
        Task<bool> UserHasRoleAsync(string uid, string role);

        /// <summary>
        /// Get user's role from their profile
        /// </summary>
        Task<string> GetUserRoleAsync(string uid);
    }
}
