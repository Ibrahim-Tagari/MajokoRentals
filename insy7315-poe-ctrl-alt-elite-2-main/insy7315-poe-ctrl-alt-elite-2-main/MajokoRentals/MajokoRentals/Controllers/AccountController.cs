using MajokoRentals.Core.ViewModels;
using MajokoRentals.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FirebaseAdmin.Auth;

namespace MajokoRentals.Controllers
{
    public class AccountController : Controller
    {
        private readonly IFirebaseMvcAuthService _firebaseAuth;
        private readonly IUserProfileService _userProfile;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly IFileUploadService _fileUploadService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IFirebaseMvcAuthService firebaseAuth,
            IUserProfileService userProfile,
            IEmailService emailService,
            IEmailTemplateService emailTemplateService,
            IFileUploadService fileUploadService,
            ILogger<AccountController> logger)
        {
            _firebaseAuth = firebaseAuth;
            _userProfile = userProfile;
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
            _fileUploadService = fileUploadService;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // This method receives the Firebase ID token from the client-side Firebase authentication
            // The actual authentication happens on the client using Firebase JS SDK
            // This endpoint just validates the token and creates the session

            if (string.IsNullOrEmpty(model.FirebaseIdToken))
            {
                ModelState.AddModelError(string.Empty, "Authentication failed. Please try again.");
                return View(model);
            }

            try
            {
                // Verify the Firebase ID token
                FirebaseToken decodedToken = await _firebaseAuth.VerifyIdTokenAsync(model.FirebaseIdToken);

                // Get user profile from Firestore
                var profile = await _firebaseAuth.GetUserProfileAsync(decodedToken.Uid);

                if (profile == null || !profile.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "Account not found or inactive.");
                    return View(model);
                }

                // Create claims for the session
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, decodedToken.Uid),
                    new Claim(ClaimTypes.Email, profile.Email),
                    new Claim(ClaimTypes.Name, $"{profile.FirstName} {profile.LastName}"),
                    new Claim(ClaimTypes.Role, profile.Role),
                    new Claim("firebase_uid", decodedToken.Uid),
                    new Claim("firebase_token", model.FirebaseIdToken)
                };

                // Add profile image if available
                if (!string.IsNullOrEmpty(profile.ProfileImageUrl))
                {
                    claims.Add(new Claim("profile_image", profile.ProfileImageUrl));
                }

                var identity = new ClaimsIdentity(claims, "Firebase");
                var principal = new ClaimsPrincipal(identity);

                // Sign in the user with cookie authentication
                await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                });

                _logger.LogInformation($"User logged in: {profile.Email}");

                // Store Firebase token in session for API calls
                HttpContext.Session.SetString("FirebaseToken", model.FirebaseIdToken);

                // Redirect based on role
                if (profile.Role == "Admin" || profile.Role == "PropertyManager")
                {
                    return RedirectToAction("Index", "Dashboard");
                }
                else if (profile.Role == "Caretaker")
                {
                    return RedirectToAction("Index", "Maintenance");
                }
                else if (profile.Role == "Tenant")
                {
                    return RedirectToAction("Index", "Properties");
                }
                else // Applicant
                {
                    return RedirectToAction("Index", "Properties");
                }
            }
            catch (FirebaseAuthException ex)
            {
                _logger.LogError(ex, "Firebase authentication error during login");
                ModelState.AddModelError(string.Empty, "Invalid login credentials.");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
                return View(model);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            // This method receives the Firebase ID token after client-side registration
            // The actual user creation happens on the client using Firebase JS SDK
            // This endpoint creates the user profile in Firestore

            if (string.IsNullOrEmpty(model.FirebaseIdToken))
            {
                ModelState.AddModelError(string.Empty, "Registration failed. Please try again.");
                return View(model);
            }

            try
            {
                // Verify the Firebase ID token
                FirebaseToken decodedToken = await _firebaseAuth.VerifyIdTokenAsync(model.FirebaseIdToken);

                // Check if user profile already exists
                var existingProfile = await _userProfile.GetUserProfileAsync(decodedToken.Uid);
                if (existingProfile != null)
                {
                    ModelState.AddModelError(string.Empty, "User already registered.");
                    return View(model);
                }

                // Create user profile in Firestore
                var profile = new UserProfile
                {
                    Uid = decodedToken.Uid,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.PhoneNumber,
                    Role = model.Role,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _userProfile.CreateUserProfileAsync(decodedToken.Uid, profile);

                // Set custom claims for role
                await _firebaseAuth.UpdateUserRoleAsync(decodedToken.Uid, model.Role);

                _logger.LogInformation($"User registered: {model.Email} with role: {model.Role}");

                // Create claims and sign in
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, decodedToken.Uid),
                    new Claim(ClaimTypes.Email, profile.Email),
                    new Claim(ClaimTypes.Name, $"{profile.FirstName} {profile.LastName}"),
                    new Claim(ClaimTypes.Role, profile.Role),
                    new Claim("firebase_uid", decodedToken.Uid),
                    new Claim("firebase_token", model.FirebaseIdToken)
                };

                // Add profile image if available
                if (!string.IsNullOrEmpty(profile.ProfileImageUrl))
                {
                    claims.Add(new Claim("profile_image", profile.ProfileImageUrl));
                }

                var identity = new ClaimsIdentity(claims, "Firebase");
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                });

                // Store Firebase token in session
                HttpContext.Session.SetString("FirebaseToken", model.FirebaseIdToken);

                TempData["SuccessMessage"] = "Registration successful! Welcome to Dr Majoko Projects.";

                // Redirect based on role
                if (model.Role == "Tenant" || model.Role == "Applicant")
                {
                    return RedirectToAction("Index", "Properties");
                }
                else
                {
                    return RedirectToAction("Index", "Dashboard");
                }
            }
            catch (FirebaseAuthException ex)
            {
                _logger.LogError(ex, "Firebase authentication error during registration");
                ModelState.AddModelError(string.Empty, "Registration failed. Please try again.");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                ModelState.AddModelError(string.Empty, "An error occurred during registration. Please try again.");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("Cookies");
            HttpContext.Session.Clear();
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // Admin: Create User Account
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult CreateUser()
        {
            return View();
        }

        // Admin: Create User via Backend (using Firebase Admin SDK)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUserViaAdmin([FromBody] AdminCreateUserRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password) ||
                    string.IsNullOrEmpty(request.FirstName) || string.IsNullOrEmpty(request.LastName) ||
                    string.IsNullOrEmpty(request.Role))
                {
                    return Json(new { success = false, message = "All required fields must be filled." });
                }

                if (request.Password.Length < 6)
                {
                    return Json(new { success = false, message = "Password must be at least 6 characters long." });
                }

                // Create user in Firebase using the service
                var uid = await _firebaseAuth.RegisterUserAsync(
                    request.Email,
                    request.Password,
                    request.FirstName,
                    request.LastName,
                    request.Role
                );

                _logger.LogInformation($"Admin created user: {request.Email} with role: {request.Role}");

                // Send welcome email if requested
                if (request.SendWelcomeEmail)
                {
                    try
                    {
                        var loginUrl = $"{Request.Scheme}://{Request.Host}/Account/Login";
                        var emailBody = _emailTemplateService.GetWelcomeEmailTemplate(
                            request.FirstName,
                            request.LastName,
                            request.Email,
                            request.Password,
                            request.Role,
                            loginUrl
                        );

                        await _emailService.SendEmailAsync(
                            request.Email,
                            "Welcome to Dr Majoko Projects",
                            emailBody,
                            isHtml: true
                        );
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogWarning(emailEx, "Failed to send welcome email to: " + request.Email);
                        // Don't fail the whole operation if email fails
                    }
                }

                return Json(new
                {
                    success = true,
                    message = $"User account created successfully! {(request.SendWelcomeEmail ? "Welcome email sent." : "")}"
                });
            }
            catch (FirebaseAuthException ex)
            {
                _logger.LogError(ex, "Firebase error creating user");
                return Json(new { success = false, message = GetFirebaseErrorMessage(ex.AuthErrorCode.ToString()) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return Json(new { success = false, message = "An error occurred while creating the user account." });
            }
        }

        private string GetFirebaseErrorMessage(string errorCode)
        {
            return errorCode switch
            {
                "EmailAlreadyExists" => "An account with this email already exists.",
                "InvalidEmail" => "Invalid email address.",
                "WeakPassword" => "Password is too weak.",
                _ => "Failed to create user account. Please try again."
            };
        }

        // GET: Account/Profile
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction(nameof(Login));
                }

                var userProfile = await _userProfile.GetUserProfileAsync(userId);

                if (userProfile == null)
                {
                    TempData["ErrorMessage"] = "Unable to load profile. Please try again.";
                    return RedirectToAction("Index", "Home");
                }

                var viewModel = new ProfileViewModel
                {
                    Uid = userProfile.Uid,
                    Email = userProfile.Email,
                    FirstName = userProfile.FirstName,
                    LastName = userProfile.LastName,
                    PhoneNumber = userProfile.PhoneNumber,
                    Role = userProfile.Role,
                    ProfileImageUrl = userProfile.ProfileImageUrl,
                    CreatedAt = userProfile.CreatedAt
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user profile");
                TempData["ErrorMessage"] = "Unable to load profile. Please try again.";
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: Account/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Profile(ProfileViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId) || userId != viewModel.Uid)
                {
                    return Forbid();
                }

                var userProfile = await _userProfile.GetUserProfileAsync(userId);
                if (userProfile == null)
                {
                    TempData["ErrorMessage"] = "Profile not found.";
                    return RedirectToAction("Index", "Home");
                }

                // Update profile fields
                userProfile.FirstName = viewModel.FirstName;
                userProfile.LastName = viewModel.LastName;
                userProfile.PhoneNumber = viewModel.PhoneNumber;
                userProfile.UpdatedAt = DateTime.UtcNow;

                // Handle profile image upload if provided
                if (viewModel.NewProfileImage != null)
                {
                    try
                    {
                        // Validate the file
                        if (!_fileUploadService.IsValidFile(viewModel.NewProfileImage))
                        {
                            ModelState.AddModelError("NewProfileImage", "Invalid file type. Please upload an image file (JPG, PNG, GIF).");
                            return View(viewModel);
                        }

                        // Delete old profile image if it exists
                        if (!string.IsNullOrEmpty(userProfile.ProfileImageUrl))
                        {
                            await _fileUploadService.DeleteFileAsync(userProfile.ProfileImageUrl);
                        }

                        // Upload new profile image
                        var imageUrl = await _fileUploadService.UploadFileAsync(viewModel.NewProfileImage, "profiles");
                        userProfile.ProfileImageUrl = imageUrl;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading profile image");
                        ModelState.AddModelError("NewProfileImage", "Failed to upload profile image. Please try again.");
                        return View(viewModel);
                    }
                }

                await _userProfile.UpdateUserProfileAsync(userId, userProfile);

                // Refresh claims to update profile image and name in navigation
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userProfile.Uid),
                    new Claim(ClaimTypes.Email, userProfile.Email),
                    new Claim(ClaimTypes.Name, $"{userProfile.FirstName} {userProfile.LastName}"),
                    new Claim(ClaimTypes.Role, userProfile.Role),
                    new Claim("firebase_uid", userProfile.Uid),
                    new Claim("firebase_token", HttpContext.Session.GetString("FirebaseToken") ?? "")
                };

                // Add profile image if available
                if (!string.IsNullOrEmpty(userProfile.ProfileImageUrl))
                {
                    claims.Add(new Claim("profile_image", userProfile.ProfileImageUrl));
                }

                var identity = new ClaimsIdentity(claims, "Firebase");
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties
                {
                    IsPersistent = User.Identity?.AuthenticationType == "Firebase",
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                });

                TempData["SuccessMessage"] = "Profile updated successfully!";
                return RedirectToAction(nameof(Profile));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile");
                ModelState.AddModelError("", "An error occurred while updating your profile. Please try again.");
                return View(viewModel);
            }
        }
    }

    // Request model for admin user creation
    public class AdminCreateUserRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool SendWelcomeEmail { get; set; }
    }
}

