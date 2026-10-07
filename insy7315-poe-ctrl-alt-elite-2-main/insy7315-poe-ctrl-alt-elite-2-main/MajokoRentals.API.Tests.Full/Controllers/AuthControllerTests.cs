using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MajokoRentals.API.Controllers;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
// disambiguate SignInResult
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace MajokoRentals.API.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<SignInManager<ApplicationUser>> _signInManager;
        private readonly Mock<IConfiguration> _config;
        private readonly Mock<IFirebaseAuthService> _firebaseAuth;
        private readonly Mock<IFirestoreService> _firestore;
        private readonly Mock<ILogger<AuthController>> _logger;

        public AuthControllerTests()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();

            // the real UserManager ctor wants a bunch of non-null services
            // use null! to tell the compiler this is intentional for tests
            _userManager = new Mock<UserManager<ApplicationUser>>(
                store.Object,
                null!,   // IOptions<IdentityOptions>
                null!,   // IPasswordHasher<ApplicationUser>
                null!,   // IEnumerable<IUserValidator<ApplicationUser>>
                null!,   // IEnumerable<IPasswordValidator<ApplicationUser>>
                null!,   // ILookupNormalizer
                null!,   // IdentityErrorDescriber
                null!,   // IServiceProvider
                null!    // ILogger<UserManager<ApplicationUser>>
            );

            var httpContext = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();

            _signInManager = new Mock<SignInManager<ApplicationUser>>(
                _userManager.Object,
                httpContext.Object,
                claimsFactory.Object,
                null!,   // IOptions<IdentityOptions>
                null!,   // ILogger<SignInManager<ApplicationUser>>
                null!,   // IAuthenticationSchemeProvider
                null!    // IUserConfirmation<ApplicationUser>
            );

            _config = new Mock<IConfiguration>();
            _config.Setup(c => c["Jwt:SecretKey"]).Returns("THIS_IS_A_TEST_SECRET_KEY_1234567890");
            _config.Setup(c => c["Jwt:Issuer"]).Returns("test-issuer");
            _config.Setup(c => c["Jwt:Audience"]).Returns("test-audience");
            _config.Setup(c => c["Jwt:ExpirationInMinutes"]).Returns("60");

            _firebaseAuth = new Mock<IFirebaseAuthService>();
            _firestore = new Mock<IFirestoreService>();

            // controller is tolerant of these failing, so make them always throw
            _firebaseAuth.Setup(x => x.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("firebase off in tests"));

            _firebaseAuth.Setup(x => x.SetCustomUserClaimsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>() ))
                .ThrowsAsync(new Exception("firebase off in tests"));

            _firestore.Setup(x => x.AddDocumentAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ThrowsAsync(new Exception("firestore off in tests"));

            _logger = new Mock<ILogger<AuthController>>();
        }

        private AuthController CreateController(ClaimsPrincipal? user = null)
        {
            var controller = new AuthController(
                _userManager.Object,
                _signInManager.Object,
                _config.Object,
                _firebaseAuth.Object,
                _firestore.Object,
                _logger.Object
            );

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            return controller;
        }

        // helper to read anonymous-object properties safely (no dynamic)
        private static T? Prop<T>(object obj, string name)
        {
            var p = obj.GetType().GetProperty(name);
            if (p == null) return default;
            return (T?)p.GetValue(obj);
        }

        // ------------------ REGISTER ------------------

        [Fact]
        public async Task Register_InvalidModel_ReturnsBadRequest()
        {
            var controller = CreateController();
            controller.ModelState.AddModelError("Email", "required");

            var result = await controller.Register(new RegisterDto());

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Register_EmailAlreadyExists_ReturnsBadRequest()
        {
            var dto = new RegisterDto
            {
                Email = "exists@test.com",
                Password = "Pass123!",
                FirstName = "Test",
                LastName = "User"
            };

            _userManager.Setup(x => x.FindByEmailAsync(dto.Email))
                .ReturnsAsync(new ApplicationUser
                {
                    Id = "user-1",
                    Email = dto.Email,
                    UserName = dto.Email,
                    IsActive = true
                });

            var controller = CreateController();

            var result = await controller.Register(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var val = bad.Value!;
            var msg = Prop<string>(val, "message");
            Assert.Equal("Email already registered", msg);
        }

        [Fact]
        public async Task Register_Success_WhenFirebaseFails_StillReturnsToken()
        {
            var dto = new RegisterDto
            {
                Email = "new@test.com",
                Password = "Pass123!",
                FirstName = "New",
                LastName = "User",
                Role = "Applicant"
            };

            _userManager.Setup(x => x.FindByEmailAsync(dto.Email))
                .ReturnsAsync((ApplicationUser?)null);

            _userManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), dto.Password))
                .ReturnsAsync(IdentityResult.Success);

            _userManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            _userManager.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(new List<string> { "Applicant" });

            var controller = CreateController();

            var result = await controller.Register(dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            var auth = Assert.IsType<AuthResponseDto>(ok.Value);
            Assert.Equal("new@test.com", auth.Email);
            Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        }

        // ------------------ LOGIN ------------------

        [Fact]
        public async Task Login_InvalidModel_ReturnsBadRequest()
        {
            var controller = CreateController();
            controller.ModelState.AddModelError("Email", "required");

            var result = await controller.Login(new LoginDto());

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Login_UserNotFound_ReturnsUnauthorized_WithMessage()
        {
            var dto = new LoginDto { Email = "nouser@test.com", Password = "Pass123!" };

            _userManager.Setup(x => x.FindByEmailAsync(dto.Email))
                .ReturnsAsync((ApplicationUser?)null);

            var controller = CreateController();

            var result = await controller.Login(dto);

            var unauth = Assert.IsType<UnauthorizedObjectResult>(result);
            var val = unauth.Value!;
            var msg = Prop<string>(val, "message");
            Assert.Equal("Invalid credentials", msg);
        }

        [Fact]
        public async Task Login_UserInactive_ReturnsUnauthorized()
        {
            var dto = new LoginDto { Email = "inactive@test.com", Password = "Pass123!" };

            _userManager.Setup(x => x.FindByEmailAsync(dto.Email))
                .ReturnsAsync(new ApplicationUser
                {
                    Id = "u1",
                    Email = dto.Email,
                    UserName = dto.Email,
                    IsActive = false
                });

            var controller = CreateController();

            var result = await controller.Login(dto);

            var unauth = Assert.IsType<UnauthorizedObjectResult>(result);
            var val = unauth.Value!;
            var msg = Prop<string>(val, "message");
            Assert.Equal("Invalid credentials", msg);
        }

        [Fact]
        public async Task Login_BadPassword_ReturnsUnauthorized()
        {
            var dto = new LoginDto { Email = "user@test.com", Password = "Wrong!" };

            var user = new ApplicationUser
            {
                Id = "u2",
                Email = dto.Email,
                UserName = dto.Email,
                IsActive = true
            };

            _userManager.Setup(x => x.FindByEmailAsync(dto.Email))
                .ReturnsAsync(user);

            _signInManager.Setup(x => x.CheckPasswordSignInAsync(user, dto.Password, false))
                .ReturnsAsync(IdentitySignInResult.Failed);

            var controller = CreateController();

            var result = await controller.Login(dto);

            var unauth = Assert.IsType<UnauthorizedObjectResult>(result);
            var val = unauth.Value!;
            var msg = Prop<string>(val, "message");
            Assert.Equal("Invalid credentials", msg);
        }

        [Fact]
        public async Task Login_Success_ReturnsOkWithToken()
        {
            var dto = new LoginDto { Email = "user@test.com", Password = "Pass123!" };

            var user = new ApplicationUser
            {
                Id = "u3",
                Email = dto.Email,
                UserName = dto.Email,
                IsActive = true
            };

            _userManager.Setup(x => x.FindByEmailAsync(dto.Email))
                .ReturnsAsync(user);

            _signInManager.Setup(x => x.CheckPasswordSignInAsync(user, dto.Password, false))
                .ReturnsAsync(IdentitySignInResult.Success);

            _userManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Applicant" });

            var controller = CreateController();

            var result = await controller.Login(dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            var auth = Assert.IsType<AuthResponseDto>(ok.Value);
            Assert.Equal("user@test.com", auth.Email);
            Assert.Contains("Applicant", auth.Roles);
            Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        }

        // ------------------ GET /api/auth/me ------------------

        [Fact]
        public async Task GetCurrentUser_NoUserId_ReturnsUnauthorized()
        {
            var controller = CreateController(); // no claims

            var result = await controller.GetCurrentUser();

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task GetCurrentUser_UserFound_ReturnsOk()
        {
            var userId = "me-1";
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId),
                        new Claim(ClaimTypes.Email, "me@test.com")
                    },
                    "TestAuth"
                )
            );

            var controller = CreateController(principal);

            var appUser = new ApplicationUser
            {
                Id = userId,
                Email = "me@test.com",
                FirstName = "Me",
                LastName = "User",
                PhoneNumber = "0123"
            };

            _userManager.Setup(x => x.FindByIdAsync(userId))
                .ReturnsAsync(appUser);

            _userManager.Setup(x => x.GetRolesAsync(appUser))
                .ReturnsAsync(new List<string> { "Applicant" });

            var result = await controller.GetCurrentUser();

            var ok = Assert.IsType<OkObjectResult>(result);
            var val = ok.Value!;

            var id = Prop<string>(val, "id");
            var email = Prop<string>(val, "email");
            var firstName = Prop<string>(val, "firstName");
            var roles = Prop<IEnumerable<string>>(val, "roles");

            Assert.Equal(userId, id);
            Assert.Equal("me@test.com", email);
            Assert.Equal("Me", firstName);
            Assert.Contains("Applicant", roles!);
        }
    }
}
