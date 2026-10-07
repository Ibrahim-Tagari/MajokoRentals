using Microsoft.Extensions.Logging;

namespace MajokoRentals.Core.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly IFirestoreService _firestore;
        private readonly ILogger<UserProfileService> _logger;
        private const string USERS_COLLECTION = "users";

        public UserProfileService(IFirestoreService firestore, ILogger<UserProfileService> logger)
        {
            _firestore = firestore;
            _logger = logger;
        }

        public async Task<UserProfile?> GetUserProfileAsync(string uid)
        {
            try
            {
                var userDict = await _firestore.GetDocumentAsync<Dictionary<string, object>>(USERS_COLLECTION, uid);

                if (userDict == null)
                    return null;

                return MapToUserProfile(userDict);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user profile: {uid}");
                return null;
            }
        }

        public async Task<UserProfile?> GetUserProfileByEmailAsync(string email)
        {
            try
            {
                var allUsers = await _firestore.GetCollectionAsync<Dictionary<string, object>>(USERS_COLLECTION);
                var userDict = allUsers.FirstOrDefault(u =>
                    u.ContainsKey("email") && u["email"].ToString()!.Equals(email, StringComparison.OrdinalIgnoreCase));

                if (userDict == null)
                    return null;

                return MapToUserProfile(userDict);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user profile by email: {email}");
                return null;
            }
        }

        public async Task CreateUserProfileAsync(string uid, UserProfile profile)
        {
            try
            {
                profile.Uid = uid;
                profile.CreatedAt = DateTime.UtcNow;

                var profileDict = new Dictionary<string, object>
                {
                    { "uid", profile.Uid },
                    { "email", profile.Email },
                    { "firstName", profile.FirstName },
                    { "lastName", profile.LastName },
                    { "phoneNumber", profile.PhoneNumber ?? "" },
                    { "role", profile.Role },
                    { "isActive", profile.IsActive },
                    { "createdAt", profile.CreatedAt.ToString("o") },
                    { "profileImageUrl", profile.ProfileImageUrl ?? "" },
                    { "preferredLanguage", profile.PreferredLanguage.ToString() },
                    { "theme", profile.Theme.ToString() }
                };

                await _firestore.UpdateDocumentAsync(USERS_COLLECTION, uid, profileDict);
                _logger.LogInformation($"User profile created: {uid}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating user profile: {uid}");
                throw;
            }
        }

        public async Task UpdateUserProfileAsync(string uid, UserProfile profile)
        {
            try
            {
                profile.UpdatedAt = DateTime.UtcNow;

                var profileDict = new Dictionary<string, object>
                {
                    { "uid", profile.Uid },
                    { "email", profile.Email },
                    { "firstName", profile.FirstName },
                    { "lastName", profile.LastName },
                    { "phoneNumber", profile.PhoneNumber ?? "" },
                    { "role", profile.Role },
                    { "isActive", profile.IsActive },
                    { "updatedAt", profile.UpdatedAt.Value.ToString("o") },
                    { "profileImageUrl", profile.ProfileImageUrl ?? "" },
                    { "preferredLanguage", profile.PreferredLanguage.ToString() },
                    { "theme", profile.Theme.ToString() }
                };

                await _firestore.UpdateDocumentAsync(USERS_COLLECTION, uid, profileDict);
                _logger.LogInformation($"User profile updated: {uid}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user profile: {uid}");
                throw;
            }
        }

        public async Task DeleteUserProfileAsync(string uid)
        {
            try
            {
                await _firestore.DeleteDocumentAsync(USERS_COLLECTION, uid);
                _logger.LogInformation($"User profile deleted: {uid}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting user profile: {uid}");
                throw;
            }
        }

        public async Task<List<UserProfile>> GetUsersByRoleAsync(string role)
        {
            try
            {
                var allUsers = await _firestore.GetCollectionAsync<Dictionary<string, object>>(USERS_COLLECTION);
                return allUsers
                    .Where(u => u.ContainsKey("role") && u["role"].ToString()!.Equals(role, StringComparison.OrdinalIgnoreCase))
                    .Select(MapToUserProfile)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting users by role: {role}");
                return new List<UserProfile>();
            }
        }

        public async Task<bool> UserHasRoleAsync(string uid, string role)
        {
            try
            {
                var profile = await GetUserProfileAsync(uid);
                return profile != null && profile.Role.Equals(role, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking user role: {uid}");
                return false;
            }
        }

        private UserProfile MapToUserProfile(Dictionary<string, object> dict)
        {
            return new UserProfile
            {
                Uid = dict.ContainsKey("uid") ? dict["uid"].ToString()! : "",
                Email = dict.ContainsKey("email") ? dict["email"].ToString()! : "",
                FirstName = dict.ContainsKey("firstName") ? dict["firstName"].ToString()! : "",
                LastName = dict.ContainsKey("lastName") ? dict["lastName"].ToString()! : "",
                PhoneNumber = dict.ContainsKey("phoneNumber") ? dict["phoneNumber"].ToString() : null,
                Role = dict.ContainsKey("role") ? dict["role"].ToString()! : "Applicant",
                IsActive = dict.ContainsKey("isActive") && Convert.ToBoolean(dict["isActive"]),
                CreatedAt = dict.ContainsKey("createdAt") ? ParseFirestoreTimestamp(dict["createdAt"]) : DateTime.UtcNow,
                UpdatedAt = dict.ContainsKey("updatedAt") ? ParseFirestoreTimestampNullable(dict["updatedAt"]) : null,
                ProfileImageUrl = dict.ContainsKey("profileImageUrl") ? dict["profileImageUrl"].ToString() : null,
                PreferredLanguage = dict.ContainsKey("preferredLanguage") ?
                    Enum.Parse<Models.Language>(dict["preferredLanguage"].ToString()!) : Models.Language.English,
                Theme = dict.ContainsKey("theme") ?
                    Enum.Parse<Models.Theme>(dict["theme"].ToString()!) : Models.Theme.Light
            };
        }

        private DateTime ParseFirestoreTimestamp(object value)
        {
            if (value == null)
                return DateTime.UtcNow;

            var stringValue = value.ToString()!;

            // Handle Firestore Timestamp format: "Timestamp: 2025-10-20T20:34:04.389953Z"
            if (stringValue.StartsWith("Timestamp: ", StringComparison.OrdinalIgnoreCase))
            {
                stringValue = stringValue.Substring("Timestamp: ".Length);
            }

            if (DateTime.TryParse(stringValue, out DateTime result))
            {
                return result;
            }

            return DateTime.UtcNow;
        }

        private DateTime? ParseFirestoreTimestampNullable(object value)
        {
            if (value == null)
                return null;

            var stringValue = value.ToString()!;

            // Handle Firestore Timestamp format: "Timestamp: 2025-10-20T20:34:04.389953Z"
            if (stringValue.StartsWith("Timestamp: ", StringComparison.OrdinalIgnoreCase))
            {
                stringValue = stringValue.Substring("Timestamp: ".Length);
            }

            if (DateTime.TryParse(stringValue, out DateTime result))
            {
                return result;
            }

            return null;
        }
    }
}
