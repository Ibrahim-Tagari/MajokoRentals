using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Services;
using System.Security.Claims;

namespace MajokoRentals.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IFirestoreService _firestore;
        private readonly IFirebaseAuthService _firebaseAuth;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            IFirestoreService firestore,
            IFirebaseAuthService firebaseAuth,
            ILogger<UsersController> logger)
        {
            _firestore = firestore;
            _firebaseAuth = firebaseAuth;
            _logger = logger;
        }

        /// <summary>
        /// Create user profile in Firestore (for SSO users)
        /// </summary>
        [HttpPost("profile")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateUserProfile([FromBody] CreateUserProfileDto dto)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Check if profile already exists
                var existingProfile = await _firestore.GetDocumentAsync<Dictionary<string, object>>("users", userId);
                if (existingProfile != null)
                {
                    // Profile already exists, return it
                    var existingUserDto = new UserDto
                    {
                        Id = userId,
                        Email = dto.Email,
                        FirstName = existingProfile.ContainsKey("firstName") ? existingProfile["firstName"].ToString()! : dto.FirstName,
                        LastName = existingProfile.ContainsKey("lastName") ? existingProfile["lastName"].ToString()! : dto.LastName,
                        PhoneNumber = existingProfile.ContainsKey("phoneNumber") ? existingProfile["phoneNumber"].ToString() : null,
                        Role = existingProfile.ContainsKey("role") ? existingProfile["role"].ToString()! : "Tenant",
                        IsActive = existingProfile.ContainsKey("isActive") ? Convert.ToBoolean(existingProfile["isActive"]) : true
                    };
                    return Ok(existingUserDto);
                }

                // Create new profile
                var userProfile = new Dictionary<string, object>
                {
                    { "uid", userId },
                    { "email", dto.Email },
                    { "firstName", dto.FirstName },
                    { "lastName", dto.LastName },
                    { "phoneNumber", dto.PhoneNumber ?? "" },
                    { "role", dto.Role ?? "Tenant" },
                    { "isActive", true },
                    { "createdAt", DateTime.UtcNow.ToString("o") },
                    { "createdVia", "Mobile-SSO" }
                };

                await _firestore.UpdateDocumentAsync("users", userId, userProfile);

                _logger.LogInformation($"User profile {userId} created in Firestore via SSO");

                // CRITICAL: Set Firebase custom claims so role appears in ID token
                var role = dto.Role ?? "Tenant";
                try
                {
                    await _firebaseAuth.SetCustomUserClaimsAsync(userId, new Dictionary<string, object>
                    {
                        { "role", role }
                    });
                    _logger.LogInformation($"Custom claims set for user {userId} with role {role}");
                }
                catch (Exception claimsEx)
                {
                    _logger.LogError(claimsEx, $"Error setting custom claims for user {userId}. User will need to re-login.");
                }

                var userDto = new UserDto
                {
                    Id = userId,
                    Email = dto.Email,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    PhoneNumber = dto.PhoneNumber,
                    Role = role,
                    IsActive = true
                };

                return CreatedAtAction(nameof(GetUser), new { id = userId }, userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user profile in Firestore");
                return StatusCode(500, new { message = "Error creating user profile" });
            }
        }

        /// <summary>
        /// Get all users (Admin only)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(IEnumerable<UserListDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsers([FromQuery] string? role = null)
        {
            try
            {
                var allUsers = await _firestore.GetCollectionAsync<Dictionary<string, object>>("users");

                var filteredUsers = allUsers.Where(u =>
                {
                    if (!string.IsNullOrEmpty(role) && u.ContainsKey("role"))
                    {
                        return u["role"].ToString()?.Equals(role, StringComparison.OrdinalIgnoreCase) == true;
                    }
                    return true;
                });

                var users = filteredUsers.Select(u => new UserListDto
                {
                    Id = u.ContainsKey("uid") ? u["uid"].ToString()! : "",
                    Email = u.ContainsKey("email") ? u["email"].ToString()! : "",
                    FirstName = u.ContainsKey("firstName") ? u["firstName"].ToString()! : "",
                    LastName = u.ContainsKey("lastName") ? u["lastName"].ToString()! : "",
                    PhoneNumber = u.ContainsKey("phoneNumber") ? u["phoneNumber"].ToString() : null,
                    Role = u.ContainsKey("role") ? u["role"].ToString()! : "",
                    IsActive = u.ContainsKey("isActive") ? Convert.ToBoolean(u["isActive"]) : true,
                    CreatedAt = u.ContainsKey("createdAt") ? DateTime.Parse(u["createdAt"].ToString()!) : DateTime.UtcNow
                }).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users from Firestore");
                return StatusCode(500, new { message = "Error fetching users" });
            }
        }

        /// <summary>
        /// Get all caretakers (for assigning maintenance requests)
        /// </summary>
        [HttpGet("caretakers")]
        [Authorize(Roles = "Admin,PropertyManager")]
        [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCaretakers()
        {
            try
            {
                var allUsers = await _firestore.GetCollectionAsync<Dictionary<string, object>>("users");

                var caretakers = allUsers
                    .Where(u => u.ContainsKey("role") &&
                                u["role"].ToString()?.Equals("Caretaker", StringComparison.OrdinalIgnoreCase) == true &&
                                (!u.ContainsKey("isActive") || Convert.ToBoolean(u["isActive"])))
                    .Select(u => new UserDto
                    {
                        Id = u.ContainsKey("uid") ? u["uid"].ToString()! : "",
                        Email = u.ContainsKey("email") ? u["email"].ToString()! : "",
                        FirstName = u.ContainsKey("firstName") ? u["firstName"].ToString()! : "",
                        LastName = u.ContainsKey("lastName") ? u["lastName"].ToString()! : "",
                        PhoneNumber = u.ContainsKey("phoneNumber") ? u["phoneNumber"].ToString() : null,
                        Role = u.ContainsKey("role") ? u["role"].ToString()! : "",
                        IsActive = u.ContainsKey("isActive") ? Convert.ToBoolean(u["isActive"]) : true
                    })
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToList();

                return Ok(caretakers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching caretakers from Firestore");
                return StatusCode(500, new { message = "Error fetching caretakers" });
            }
        }

        /// <summary>
        /// Get current user's profile
        /// </summary>
        [HttpGet("profile")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return await GetUser(userId!);
        }

        /// <summary>
        /// Update current user's profile
        /// </summary>
        [HttpPut("profile")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                var user = await _firestore.GetDocumentAsync<Dictionary<string, object>>("users", userId!);

                if (user == null)
                    return NotFound();

                user["firstName"] = dto.FirstName;
                user["lastName"] = dto.LastName;
                user["phoneNumber"] = dto.PhoneNumber ?? "";
                user["updatedAt"] = DateTime.UtcNow.ToString("o");

                await _firestore.UpdateDocumentAsync("users", userId!, user);

                _logger.LogInformation($"User profile {userId} updated in Firestore");

                // Return the updated user
                var userDto = new UserDto
                {
                    Id = user.ContainsKey("uid") ? user["uid"].ToString()! : userId!,
                    Email = user.ContainsKey("email") ? user["email"].ToString()! : "",
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    PhoneNumber = dto.PhoneNumber,
                    Role = user.ContainsKey("role") ? user["role"].ToString()! : "",
                    IsActive = user.ContainsKey("isActive") ? Convert.ToBoolean(user["isActive"]) : true
                };

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user profile {userId} in Firestore");
                return StatusCode(500, new { message = "Error updating user profile" });
            }
        }

        /// <summary>
        /// Get a specific user by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUser(string id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Users can only view their own profile unless they're admin
                if (!User.IsInRole("Admin") && userId != id)
                {
                    return Forbid();
                }

                var user = await _firestore.GetDocumentAsync<Dictionary<string, object>>("users", id);

                if (user == null)
                    return NotFound();

                var userDto = new UserDto
                {
                    Id = user.ContainsKey("uid") ? user["uid"].ToString()! : id,
                    Email = user.ContainsKey("email") ? user["email"].ToString()! : "",
                    FirstName = user.ContainsKey("firstName") ? user["firstName"].ToString()! : "",
                    LastName = user.ContainsKey("lastName") ? user["lastName"].ToString()! : "",
                    PhoneNumber = user.ContainsKey("phoneNumber") ? user["phoneNumber"].ToString() : null,
                    Role = user.ContainsKey("role") ? user["role"].ToString()! : "",
                    IsActive = user.ContainsKey("isActive") ? Convert.ToBoolean(user["isActive"]) : true
                };

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching user {id} from Firestore");
                return StatusCode(500, new { message = "Error fetching user" });
            }
        }

        /// <summary>
        /// Update user profile
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Users can only update their own profile unless they're admin
                if (!User.IsInRole("Admin") && userId != id)
                {
                    return Forbid();
                }

                var user = await _firestore.GetDocumentAsync<Dictionary<string, object>>("users", id);

                if (user == null)
                    return NotFound();

                user["firstName"] = dto.FirstName;
                user["lastName"] = dto.LastName;
                user["phoneNumber"] = dto.PhoneNumber ?? "";
                user["updatedAt"] = DateTime.UtcNow.ToString("o");

                // Only admins can change isActive status
                if (User.IsInRole("Admin"))
                {
                    user["isActive"] = dto.IsActive;
                }

                await _firestore.UpdateDocumentAsync("users", id, user);

                _logger.LogInformation($"User {id} updated in Firestore");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user {id} in Firestore");
                return StatusCode(500, new { message = "Error updating user" });
            }
        }

        /// <summary>
        /// Get users by role (Admin and PropertyManager only)
        /// </summary>
        [HttpGet("by-role/{role}")]
        [Authorize(Roles = "Admin,PropertyManager")]
        [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsersByRole(string role)
        {
            try
            {
                var allUsers = await _firestore.GetCollectionAsync<Dictionary<string, object>>("users");

                var users = allUsers
                    .Where(u => u.ContainsKey("role") &&
                                u["role"].ToString()?.Equals(role, StringComparison.OrdinalIgnoreCase) == true)
                    .Select(u => new UserDto
                    {
                        Id = u.ContainsKey("uid") ? u["uid"].ToString()! : "",
                        Email = u.ContainsKey("email") ? u["email"].ToString()! : "",
                        FirstName = u.ContainsKey("firstName") ? u["firstName"].ToString()! : "",
                        LastName = u.ContainsKey("lastName") ? u["lastName"].ToString()! : "",
                        PhoneNumber = u.ContainsKey("phoneNumber") ? u["phoneNumber"].ToString() : null,
                        Role = u.ContainsKey("role") ? u["role"].ToString()! : "",
                        IsActive = u.ContainsKey("isActive") ? Convert.ToBoolean(u["isActive"]) : true
                    })
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToList();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching users with role {role} from Firestore");
                return StatusCode(500, new { message = "Error fetching users" });
            }
        }

        /// <summary>
        /// Fix custom claims for all existing users (Development only - no auth required for initial setup)
        /// This endpoint sets Firebase custom claims for users who have roles in Firestore but not in their tokens
        /// </summary>
        [HttpPost("fix-custom-claims")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> FixCustomClaims()
        {
            try
            {
                var allUsers = await _firestore.GetCollectionAsync<Dictionary<string, object>>("users");
                var successCount = 0;
                var failureCount = 0;
                var errors = new List<string>();

                foreach (var user in allUsers)
                {
                    try
                    {
                        var uid = user.ContainsKey("uid") ? user["uid"].ToString() : null;
                        var role = user.ContainsKey("role") ? user["role"].ToString() : null;

                        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(role))
                        {
                            _logger.LogWarning($"Skipping user with missing uid or role");
                            continue;
                        }

                        await _firebaseAuth.SetCustomUserClaimsAsync(uid, new Dictionary<string, object>
                        {
                            { "role", role }
                        });

                        successCount++;
                        _logger.LogInformation($"Set custom claims for user {uid} with role {role}");
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        var errorMsg = $"Failed to set claims for user: {ex.Message}";
                        errors.Add(errorMsg);
                        _logger.LogError(ex, errorMsg);
                    }
                }

                return Ok(new
                {
                    message = "Custom claims update completed",
                    successCount = successCount,
                    failureCount = failureCount,
                    errors = errors,
                    note = "Users will need to logout and login again for the changes to take effect"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing custom claims");
                return StatusCode(500, new { message = "Error fixing custom claims" });
            }
        }
    }

    // DTOs for User operations
    public class CreateUserProfileDto
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Role { get; set; }
    }

    public class UpdateUserDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
