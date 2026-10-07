using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MajokoRentals.Core.Services
{
    public class FirebaseAuthService : IFirebaseAuthService
    {
        private readonly FirebaseAuth _firebaseAuth;
        private readonly ILogger<FirebaseAuthService> _logger;

        public FirebaseAuthService(IConfiguration configuration, ILogger<FirebaseAuthService> logger)
        {
            _logger = logger;

            try
            {
                // Initialize Firebase Admin SDK if not already initialized
                if (FirebaseApp.DefaultInstance == null)
                {
                    var serviceAccountKeyPath = configuration["Firebase:ServiceAccountKeyPath"];
                    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), serviceAccountKeyPath!);

                    FirebaseApp.Create(new AppOptions()
                    {
                        Credential = GoogleCredential.FromFile(fullPath),
                        ProjectId = configuration["Firebase:ProjectId"]
                    });

                    _logger.LogInformation("Firebase Admin SDK initialized successfully");
                }

                _firebaseAuth = FirebaseAuth.DefaultInstance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Firebase Admin SDK");
                throw;
            }
        }

        public async Task<UserRecord> CreateUserAsync(string email, string password, string displayName)
        {
            try
            {
                var args = new UserRecordArgs()
                {
                    Email = email,
                    Password = password,
                    DisplayName = displayName,
                    EmailVerified = false,
                    Disabled = false
                };

                var userRecord = await _firebaseAuth.CreateUserAsync(args);
                _logger.LogInformation($"Firebase user created successfully: {userRecord.Uid}");
                return userRecord;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating Firebase user: {email}");
                throw;
            }
        }

        public async Task<UserRecord> GetUserByEmailAsync(string email)
        {
            try
            {
                return await _firebaseAuth.GetUserByEmailAsync(email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting Firebase user by email: {email}");
                throw;
            }
        }

        public async Task<UserRecord> GetUserByIdAsync(string uid)
        {
            try
            {
                return await _firebaseAuth.GetUserAsync(uid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting Firebase user by ID: {uid}");
                throw;
            }
        }

        public async Task<string> CreateCustomTokenAsync(string uid, Dictionary<string, object>? claims = null)
        {
            try
            {
                var token = await _firebaseAuth.CreateCustomTokenAsync(uid, claims);
                _logger.LogInformation($"Custom token created for user: {uid}");
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating custom token for user: {uid}");
                throw;
            }
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

        public async Task UpdateUserAsync(string uid, UserRecordArgs args)
        {
            try
            {
                await _firebaseAuth.UpdateUserAsync(args);
                _logger.LogInformation($"Firebase user updated: {uid}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating Firebase user: {uid}");
                throw;
            }
        }

        public async Task DeleteUserAsync(string uid)
        {
            try
            {
                await _firebaseAuth.DeleteUserAsync(uid);
                _logger.LogInformation($"Firebase user deleted: {uid}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting Firebase user: {uid}");
                throw;
            }
        }

        public async Task SetCustomUserClaimsAsync(string uid, Dictionary<string, object> claims)
        {
            try
            {
                await _firebaseAuth.SetCustomUserClaimsAsync(uid, claims);
                _logger.LogInformation($"Custom claims set for user: {uid}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting custom claims for user: {uid}");
                throw;
            }
        }
    }
}
