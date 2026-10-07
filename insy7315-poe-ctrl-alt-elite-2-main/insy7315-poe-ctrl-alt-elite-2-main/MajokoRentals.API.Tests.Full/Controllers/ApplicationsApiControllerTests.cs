using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MajokoRentals.API.Controllers;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MajokoRentals.API.Tests.Controllers
{
    public class ApplicationsControllerTests
    {
        private readonly Mock<IFirestoreService> _firestoreMock;
        private readonly Mock<IUserProfileService> _userProfileMock;
        private readonly Mock<ILogger<ApplicationsController>> _loggerMock;

        public ApplicationsControllerTests()
        {
            _firestoreMock = new Mock<IFirestoreService>(MockBehavior.Strict);
            _userProfileMock = new Mock<IUserProfileService>(MockBehavior.Strict);
            _loggerMock = new Mock<ILogger<ApplicationsController>>();
        }

        // ----------------------
        // helper to build controller with user + roles
        // ----------------------
        private ApplicationsController CreateController(
            string userId = "user-123",
            params string[] roles)
        {
            var controller = new ApplicationsController(
                _firestoreMock.Object,
                _userProfileMock.Object,
                _loggerMock.Object
            );

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };

            if (roles != null)
            {
                foreach (var r in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, r));
                }
            }

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };

            return controller;
        }

        // =======================================================
        // 1. GET /api/applications  (GetApplications)
        // =======================================================

        [Fact]
        public async Task GetApplications_AsApplicant_ReturnsOnlyTheirApplications()
        {
            // Arrange
            var userId = "applicant-1";
            var controller = CreateController(userId, "Applicant");

            var firestoreData = new List<Dictionary<string, object>>
            {
                new() // belongs to this user
                {
                    ["id"] = 1L,
                    ["applicantId"] = "applicant-1",
                    ["status"] = ApplicationStatus.Submitted.ToString(),
                    ["applicationDate"] = DateTime.UtcNow.ToString("o")
                },
                new() // belongs to someone else
                {
                    ["id"] = 2L,
                    ["applicantId"] = "applicant-2",
                    ["status"] = ApplicationStatus.Submitted.ToString(),
                    ["applicationDate"] = DateTime.UtcNow.AddMinutes(-5).ToString("o")
                }
            };

            _firestoreMock
                .Setup(f => f.GetCollectionAsync<Dictionary<string, object>>("applications"))
                .ReturnsAsync(firestoreData);

            // Act
            var result = await controller.GetApplications();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);
            // should only return 1 for applicant
            Assert.Single(list);

            _firestoreMock.VerifyAll();
        }

        [Fact]
        public async Task GetApplications_WithStatusFilter_ReturnsOnlyMatchingStatus()
        {
            // Arrange
            var controller = CreateController("admin-1", "Admin");

            var firestoreData = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["id"] = 1L,
                    ["status"] = ApplicationStatus.Submitted.ToString(),
                    ["applicationDate"] = DateTime.UtcNow.ToString("o")
                },
                new()
                {
                    ["id"] = 2L,
                    ["status"] = ApplicationStatus.Approved.ToString(),
                    ["applicationDate"] = DateTime.UtcNow.AddMinutes(-1).ToString("o")
                }
            };

            _firestoreMock
                .Setup(f => f.GetCollectionAsync<Dictionary<string, object>>("applications"))
                .ReturnsAsync(firestoreData);

            // Act
            var result = await controller.GetApplications(status: ApplicationStatus.Approved);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);
            Assert.Single(list);

            _firestoreMock.VerifyAll();
        }

        [Fact]
        public async Task GetApplications_OnException_Returns500()
        {
            // Arrange
            var controller = CreateController("admin-1", "Admin");

            _firestoreMock
                .Setup(f => f.GetCollectionAsync<Dictionary<string, object>>("applications"))
                .ThrowsAsync(new Exception("boom"));

            // Act
            var result = await controller.GetApplications();

            // Assert
            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, obj.StatusCode);
        }

        // =======================================================
        // 2. GET /api/applications/{id}  (GetApplication)
        // =======================================================

        [Fact]
        public async Task GetApplication_ByNumericId_OwnerCanView_ReturnsOk()
        {
            // Arrange
            var userId = "applicant-1";
            var controller = CreateController(userId, "Applicant");

            var firestoreData = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["id"] = 123L,
                    ["applicantId"] = "applicant-1",
                    ["propertyName"] = "Test Property",
                    ["status"] = ApplicationStatus.Submitted.ToString(),
                    ["applicationDate"] = DateTime.UtcNow.ToString("o")
                }
            };

            _firestoreMock
                .Setup(f => f.GetCollectionAsync<Dictionary<string, object>>("applications"))
                .ReturnsAsync(firestoreData);

            // Act
            var result = await controller.GetApplication("123");

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = ok.Value; // we don't need to assert all props now
            Assert.NotNull(dto);

            _firestoreMock.VerifyAll();
        }

        [Fact]
        public async Task GetApplication_ByNumericId_NotOwnerAndNotAdmin_ReturnsForbid()
        {
            // Arrange
            var controller = CreateController("some-other-user", "Applicant");

            var firestoreData = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["id"] = 123L,
                    ["applicantId"] = "real-owner",
                    ["status"] = ApplicationStatus.Submitted.ToString(),
                    ["applicationDate"] = DateTime.UtcNow.ToString("o")
                }
            };

            _firestoreMock
                .Setup(f => f.GetCollectionAsync<Dictionary<string, object>>("applications"))
                .ReturnsAsync(firestoreData);

            // Act
            var result = await controller.GetApplication("123");

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task GetApplication_AsAdmin_CanViewAny_ReturnsOk()
        {
            // Arrange
            var controller = CreateController("admin-1", "Admin");

            var firestoreData = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["id"] = 999L,
                    ["applicantId"] = "another-user",
                    ["status"] = ApplicationStatus.Submitted.ToString(),
                    ["applicationDate"] = DateTime.UtcNow.ToString("o")
                }
            };

            _firestoreMock
                .Setup(f => f.GetCollectionAsync<Dictionary<string, object>>("applications"))
                .ReturnsAsync(firestoreData);

            // Act
            var result = await controller.GetApplication("999");

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task GetApplication_NotFound_Returns404()
        {
            // Arrange
            var controller = CreateController("admin-1", "Admin");

            _firestoreMock
                .Setup(f => f.GetCollectionAsync<Dictionary<string, object>>("applications"))
                .ReturnsAsync(new List<Dictionary<string, object>>());

            // Act
            var result = await controller.GetApplication("555");

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetApplication_OnException_Returns500()
        {
            // Arrange
            var controller = CreateController("admin-1", "Admin");

            _firestoreMock
                .Setup(f => f.GetCollectionAsync<Dictionary<string, object>>("applications"))
                .ThrowsAsync(new Exception("firestore down"));

            // Act
            var result = await controller.GetApplication("123");

            // Assert
            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, obj.StatusCode);
        }

        // =======================================================
        // 3. POST /api/applications  (CreateApplication)
        // =======================================================

        [Fact]
        public async Task CreateApplication_ValidRequest_ReturnsCreated()
        {
            // Arrange
            var userId = "applicant-1";
            var controller = CreateController(userId, "Applicant");

            var dto = new CreateApplicationDto
            {
                PropertyId = 7,
                MonthlyIncome = 15000,
                EmploymentStatus = "Employed",
                PhoneNumber = "0123456789"
            };

            // user profile from service
            _userProfileMock
                .Setup(u => u.GetUserProfileAsync(userId))
                .ReturnsAsync(new UserProfile
                {
                    FirstName = "Duncan",
                    LastName = "Rogers",
                    Email = "duncan@example.com",
                    PhoneNumber = "0123456789"
                });

            // properties from firestore
            _firestoreMock
                .Setup(f => f.GetCollectionAsync<Dictionary<string, object>>("properties"))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["id"] = 7L,
                        ["name"] = "Apartment 7",
                        ["address"] = "123 Main Road"
                    }
                });

            // create document
            _firestoreMock
                .Setup(f => f.AddDocumentAsync("applications", It.IsAny<Dictionary<string, object>>()))
                .ReturnsAsync("fs-doc-1");

            // Act
            var result = await controller.CreateApplication(dto);

            // Assert
            var created = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(ApplicationsController.GetApplication), created.ActionName);
            Assert.NotNull(created.Value);

            _firestoreMock.VerifyAll();
            _userProfileMock.VerifyAll();
        }

        [Fact]
        public async Task CreateApplication_UserProfileMissing_ReturnsBadRequest()
        {
            // Arrange
            var userId = "applicant-1";
            var controller = CreateController(userId, "Applicant");

            var dto = new CreateApplicationDto
            {
                PropertyId = 7,
                MonthlyIncome = 15000,
                EmploymentStatus = "Employed"
            };

            _userProfileMock
                .Setup(u => u.GetUserProfileAsync(userId))
                .ReturnsAsync((UserProfile?)null);

            // Act
            var result = await controller.CreateApplication(dto);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }

        [Fact]
        public async Task CreateApplication_InvalidModel_ReturnsBadRequest()
        {
            // Arrange
            var controller = CreateController("applicant-1", "Applicant");

            var dto = new CreateApplicationDto
            {
                // leaving out required?
                PropertyId = 0
            };

            controller.ModelState.AddModelError("EmploymentStatus", "Required");

            // Act
            var result = await controller.CreateApplication(dto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateApplication_OnException_Returns500()
        {
            // Arrange
            var userId = "applicant-1";
            var controller = CreateController(userId, "Applicant");

            var dto = new CreateApplicationDto
            {
                PropertyId = 7,
                MonthlyIncome = 15000,
                EmploymentStatus = "Employed"
            };

            _userProfileMock
                .Setup(u => u.GetUserProfileAsync(userId))
                .ReturnsAsync(new UserProfile
                {
                    FirstName = "Tester",
                    LastName = "User",
                    Email = "t@example.com"
                });

            _firestoreMock
                .Setup(f => f.GetCollectionAsync<Dictionary<string, object>>("properties"))
                .ThrowsAsync(new Exception("boom"));

            // Act
            var result = await controller.CreateApplication(dto);

            // Assert
            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, obj.StatusCode);
        }

        // =======================================================
        // 4. PUT /api/applications/{id}/review  (ReviewApplication)
        // =======================================================

        [Fact]
        public async Task ReviewApplication_Found_UpdatesAndReturnsNoContent()
        {
            // Arrange
            var controller = CreateController("pm-1", "PropertyManager");

            var dto = new ReviewApplicationDto
            {
                Status = ApplicationStatus.Approved,
                Notes = "Looks good"
            };

            // numeric path
            _firestoreMock
                .Setup(f => f.FindDocumentIdByFieldAsync("applications", "id", 123L))
                .ReturnsAsync("fs-doc-123");

            _firestoreMock
                .Setup(f => f.GetDocumentAsync<Dictionary<string, object>>("applications", "fs-doc-123"))
                .ReturnsAsync(new Dictionary<string, object>
                {
                    ["id"] = 123L,
                    ["status"] = ApplicationStatus.Submitted.ToString()
                });

            _firestoreMock
                .Setup(f => f.UpdateDocumentAsync("applications", "fs-doc-123", It.IsAny<Dictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await controller.ReviewApplication("123", dto);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _firestoreMock.VerifyAll();
        }

        [Fact]
        public async Task ReviewApplication_NotFound_Returns404()
        {
            // Arrange
            var controller = CreateController("pm-1", "PropertyManager");

            var dto = new ReviewApplicationDto
            {
                Status = ApplicationStatus.Rejected,
                Notes = "Missing docs"
            };

            _firestoreMock
                .Setup(f => f.FindDocumentIdByFieldAsync("applications", "id", 999L))
                .ReturnsAsync((string?)null);

            // Act
            var result = await controller.ReviewApplication("999", dto);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReviewApplication_OnException_Returns500()
        {
            // Arrange
            var controller = CreateController("pm-1", "PropertyManager");

            var dto = new ReviewApplicationDto
            {
                Status = ApplicationStatus.Approved
            };

            _firestoreMock
                .Setup(f => f.FindDocumentIdByFieldAsync("applications", "id", 456L))
                .ThrowsAsync(new Exception("firestore err"));

            // Act
            var result = await controller.ReviewApplication("456", dto);

            // Assert
            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, obj.StatusCode);
        }

        // =======================================================
        // 5. GET /api/applications/my-applications  (GetMyApplications)
        // =======================================================

        [Fact]
        public async Task GetMyApplications_ReturnsOnlyForCurrentUser()
        {
            // Arrange
            var userId = "applicant-1";
            var controller = CreateController(userId, "Applicant");

            _firestoreMock
                .Setup(f => f.GetCollectionAsync<Dictionary<string, object>>("applications"))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["id"] = 1L,
                        ["applicantId"] = "applicant-1",
                        ["propertyName"] = "My prop",
                        ["applicationDate"] = DateTime.UtcNow.ToString("o"),
                        ["status"] = ApplicationStatus.Submitted.ToString()
                    },
                    new()
                    {
                        ["id"] = 2L,
                        ["applicantId"] = "someone-else",
                        ["applicationDate"] = DateTime.UtcNow.ToString("o"),
                        ["status"] = ApplicationStatus.Submitted.ToString()
                    }
                });

            // Act
            var result = await controller.GetMyApplications();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);
            Assert.Single(list);

            _firestoreMock.VerifyAll();
        }

        [Fact]
        public async Task GetMyApplications_OnException_Returns500()
        {
            // Arrange
            var controller = CreateController("applicant-1", "Applicant");

            _firestoreMock
                .Setup(f => f.GetCollectionAsync<Dictionary<string, object>>("applications"))
                .ThrowsAsync(new Exception("boom"));

            // Act
            var result = await controller.GetMyApplications();

            // Assert
            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, obj.StatusCode);
        }
    }
}
