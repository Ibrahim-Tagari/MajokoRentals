using FirebaseAdmin.Auth;

namespace MajokoRentals.Core.Services
{
    public interface IFirebaseAuthService
    {
        Task<UserRecord> CreateUserAsync(string email, string password, string displayName);
        Task<UserRecord> GetUserByEmailAsync(string email);
        Task<UserRecord> GetUserByIdAsync(string uid);
        Task<string> CreateCustomTokenAsync(string uid, Dictionary<string, object>? claims = null);
        Task<FirebaseToken> VerifyIdTokenAsync(string idToken);
        Task UpdateUserAsync(string uid, UserRecordArgs args);
        Task DeleteUserAsync(string uid);
        Task SetCustomUserClaimsAsync(string uid, Dictionary<string, object> claims);
    }
}
