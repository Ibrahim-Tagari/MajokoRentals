using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Services;

namespace MajokoRentals.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IFirebaseAuthService _firebaseAuth;
        private readonly IFirestoreService _firestore;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            IFirebaseAuthService firebaseAuth,
            IFirestoreService firestore,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _firebaseAuth = firebaseAuth;
            _firestore = firestore;
            _logger = logger;
        }

        /// <summary>
        /// Login and receive JWT token
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !user.IsActive)
                return Unauthorized(new { message = "Invalid credentials" });

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!result.Succeeded)
                return Unauthorized(new { message = "Invalid credentials" });

            var token = await GenerateJwtToken(user);
            return Ok(token);
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest(new { message = "Email already registered" });

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                PhoneNumber = model.PhoneNumber,
                EmailConfirmed = true, // Auto-confirm for API users
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Create user in SQL Server
            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            // Assign role (default to Applicant if not specified)
            var role = string.IsNullOrEmpty(model.Role) ? "Applicant" : model.Role;
            if (new[] { "Tenant", "Applicant" }.Contains(role))
            {
                await _userManager.AddToRoleAsync(user, role);
            }
            else
            {
                await _userManager.AddToRoleAsync(user, "Applicant");
            }

            // Sync to Firebase
            try
            {
                // Create user in Firebase Authentication
                var firebaseUser = await _firebaseAuth.CreateUserAsync(
                    model.Email,
                    model.Password,
                    $"{model.FirstName} {model.LastName}"
                );

                _logger.LogInformation($"API: User created in Firebase with UID: {firebaseUser.Uid}");

                // Set custom claims for role-based access
                await _firebaseAuth.SetCustomUserClaimsAsync(firebaseUser.Uid, new Dictionary<string, object>
                {
                    { "role", role },
                    { "sqlId", user.Id }
                });

                // Save user data to Firestore
                await _firestore.AddDocumentAsync("users", new
                {
                    uid = firebaseUser.Uid,
                    email = model.Email,
                    firstName = model.FirstName,
                    lastName = model.LastName,
                    phoneNumber = model.PhoneNumber ?? "",
                    role = role,
                    isActive = true,
                    createdAt = DateTime.UtcNow,
                    sqlId = user.Id,
                    createdVia = "API"
                });

                _logger.LogInformation($"API: User data saved to Firestore.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Error syncing user to Firebase. User created in SQL Server only.");
                // Continue - user exists in SQL Server even if Firebase sync fails
            }

            var token = await GenerateJwtToken(user);
            return Ok(token);
        }

        /// <summary>
        /// Get current user information
        /// </summary>
        [HttpGet("me")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                phoneNumber = user.PhoneNumber,
                roles = roles
            });
        }

        private async Task<AuthResponseDto> GenerateJwtToken(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiration = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpirationInMinutes"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expiration,
                signingCredentials: creds
            );

            return new AuthResponseDto
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = roles.ToList(),
                Expiration = expiration
            };
        }
    }
}

