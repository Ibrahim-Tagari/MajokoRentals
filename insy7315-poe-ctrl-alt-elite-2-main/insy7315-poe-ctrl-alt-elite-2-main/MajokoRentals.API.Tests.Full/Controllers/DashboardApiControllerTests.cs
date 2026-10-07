using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MajokoRentals.API.Controllers;
using MajokoRentals.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MajokoRentals.API.Tests.Controllers
{
    public class DashboardApiControllerTests
    {
        private readonly Mock<IFirestoreService> _firestore;
        private readonly Mock<ILogger<DashboardApiController>> _logger;

        public DashboardApiControllerTests()
        {
            _firestore = new Mock<IFirestoreService>();
            _logger = new Mock<ILogger<DashboardApiController>>();
        }

        private DashboardApiController CreateController(ClaimsPrincipal? user = null)
        {
            var controller = new DashboardApiController(
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

        private static ClaimsPrincipal MakeUser(string userId, params string[] roles)
        {
            var claims = new List<Claim>
            {
                new Claim("user_id", userId),
                new Claim(ClaimTypes.NameIdentifier, userId)
            };

            foreach (var r in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, r));
            }

            var id = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(id);
        }

        // -------------------- ADMIN DASHBOARD --------------------

        [Fact]
        public async Task GetAdminDashboard_Success_ReturnsOkWithViewModel()
        {
            // arrange
            var adminUser = MakeUser("admin-1", "Admin");
            var controller = CreateController(adminUser);

            // properties
            var properties = new List<Dictionary<string, object>>
            {
                new() { { "id", "p1" }, { "name", "Villa 1" }, { "status", "Available" }, { "address", "1 Street" } },
                new() { { "id", "p2" }, { "name", "Villa 2" }, { "status", "Occupied" } },
                new() { { "id", "p3" }, { "name", "Villa 3" }, { "status", "Available" } }
            };

            // leases
            var leases = new List<Dictionary<string, object>>
            {
                new()
                {
                    { "id", "l1" },
                    { "propertyId", "p2" },
                    { "status", "Active" },
                    { "monthlyRent", 12000 },
                    { "startDate", DateTime.UtcNow.AddDays(-10) },
                    { "endDate", DateTime.UtcNow.AddDays(30) }
                },
                new()
                {
                    { "id", "l2" },
                    { "propertyId", "p1" },
                    { "status", "Inactive" },
                    { "monthlyRent", 7000 }
                }
            };

            // invoices
            var invoices = new List<Dictionary<string, object>>
            {
                new() { { "id", "inv1" }, { "status", "Paid" }, { "amount", 5000.0 }, { "dueDate", DateTime.UtcNow } },
                new() { { "id", "inv2" }, { "status", "Pending" }, { "amount", 3000.0 }, { "dueDate", DateTime.UtcNow } },
                new() { { "id", "inv3" }, { "status", "Overdue" }, { "amount", 2000.0 }, { "dueDate", DateTime.UtcNow.AddDays(-3) } }
            };

            // applications
            var applications = new List<Dictionary<string, object>>
            {
                new() { { "id", "a1" }, { "status", "Pending" }, { "propertyId", "p1" }, { "createdAt", DateTime.UtcNow } },
                new() { { "id", "a2" }, { "status", "Approved" } }
            };

            // maintenance
            var maintenance = new List<Dictionary<string, object>>
            {
                new()
                {
                    { "id", "m1" },
                    { "status", "Pending" },
                    { "createdDate", DateTime.UtcNow.AddDays(-2) },
                    { "description", "Leaky tap" },
                    { "propertyId", "p1" }
                },
                new()
                {
                    { "id", "m2" },
                    { "status", "InProgress" },
                    { "createdDate", DateTime.UtcNow.AddDays(-1) },
                    { "description", "Garden" },
                    { "propertyId", "p1" }
                },
                new()
                {
                    { "id", "m3" },
                    { "status", "Resolved" },
                    { "createdDate", DateTime.UtcNow.AddDays(-5) },
                    { "resolvedDate", DateTime.UtcNow.AddDays(-1) },
                    { "cost", 500 },
                    { "propertyId", "p2" }
                }
            };

            _firestore
                .Setup(x => x.GetCollectionAsync<Dictionary<string, object>>("properties"))
                .ReturnsAsync(properties);

            _firestore
                .Setup(x => x.GetCollectionAsync<Dictionary<string, object>>("leases"))
                .ReturnsAsync(leases);

            _firestore
                .Setup(x => x.GetCollectionAsync<Dictionary<string, object>>("invoices"))
                .ReturnsAsync(invoices);

            _firestore
                .Setup(x => x.GetCollectionAsync<Dictionary<string, object>>("applications"))
                .ReturnsAsync(applications);

            _firestore
                .Setup(x => x.GetCollectionAsync<Dictionary<string, object>>("maintenance"))
                .ReturnsAsync(maintenance);

            // act
            var result = await controller.GetAdminDashboard();

            // assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var vm = Assert.IsType<AdminDashboardViewModel>(ok.Value);

            Assert.Equal(3, vm.TotalProperties);
            Assert.Equal(2, vm.AvailableProperties); // p1, p3
            Assert.Equal(1, vm.OccupiedProperties);   // 1 active lease
            Assert.True(vm.OccupancyRate > 0);
            Assert.Equal(12000, vm.TotalMonthlyRent);
            Assert.Equal(1, vm.ActiveLeases);
            Assert.Equal(2, vm.UnpaidInvoices); // pending + overdue
            Assert.Equal(5000, vm.TotalOutstandingRent); // 3000 + 2000
            Assert.Equal(1, vm.PendingApplications);
            Assert.Equal(1, vm.PendingMaintenanceRequests);
            Assert.Equal(1, vm.InProgressMaintenanceRequests);
            Assert.True(vm.RecentActivities.Count <= 10);
            Assert.NotNull(vm.PropertyPerformance);
            Assert.True(vm.PropertyPerformance.Count <= 5);
        }

        [Fact]
        public async Task GetAdminDashboard_OnException_Returns500()
        {
            var adminUser = MakeUser("admin-1", "Admin");
            var controller = CreateController(adminUser);

            // make firestore blow up
            _firestore
                .Setup(x => x.GetCollectionAsync<Dictionary<string, object>>("properties"))
                .ThrowsAsync(new Exception("boom"));

            var result = await controller.GetAdminDashboard();

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, obj.StatusCode);
        }

        // -------------------- TENANT DASHBOARD --------------------

        [Fact]
        public async Task GetTenantDashboard_NoUserIdClaim_ReturnsUnauthorized()
        {
            var controller = CreateController(); // no claims

            var result = await controller.GetTenantDashboard();

            var unauth = Assert.IsType<UnauthorizedObjectResult>(result);
            var val = unauth.Value!;
            // shape: { message = "User ID not found in token" }
            var msgProp = val.GetType().GetProperty("message")?.GetValue(val)?.ToString();
            Assert.Equal("User ID not found in token", msgProp);
        }

        [Fact]
        public async Task GetTenantDashboard_NoActiveLease_ReturnsEmptyDashboard()
        {
            var tenant = MakeUser("tenant-1", "Tenant");
            var controller = CreateController(tenant);

            // tenant leases: none active
            _firestore
                .Setup(x => x.QueryCollectionAsync<Dictionary<string, object>>("leases", "tenantId", "tenant-1"))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new() { { "id", "l1" }, { "status", "Inactive" } }
                });

            // other collections won't be called much, but we can return empty
            _firestore
                .Setup(x => x.GetCollectionAsync<Dictionary<string, object>>("properties"))
                .ReturnsAsync(new List<Dictionary<string, object>>());

            _firestore
                .Setup(x => x.QueryCollectionAsync<Dictionary<string, object>>("invoices", "tenantId", "tenant-1"))
                .ReturnsAsync(new List<Dictionary<string, object>>());

            _firestore
                .Setup(x => x.QueryCollectionAsync<Dictionary<string, object>>("maintenance", "reportedById", "tenant-1"))
                .ReturnsAsync(new List<Dictionary<string, object>>());

            var result = await controller.GetTenantDashboard();

            var ok = Assert.IsType<OkObjectResult>(result);
            var vm = Assert.IsType<TenantDashboardViewModel>(ok.Value);

            Assert.Equal("", vm.PropertyName);
            Assert.Equal("", vm.PropertyAddress);
            Assert.Equal("", vm.PropertyId);
            Assert.Equal("", vm.LeaseId);
            Assert.Equal(0, vm.MonthlyRent);
            Assert.Equal(0, vm.OutstandingBalance);
            Assert.Equal(0, vm.UnpaidInvoices);
        }

        [Fact]
        public async Task GetTenantDashboard_WithActiveLease_ReturnsPopulated()
        {
            var tenant = MakeUser("tenant-1", "Tenant");
            var controller = CreateController(tenant);

            // ---- lease for this tenant ----
            var leaseUid = "lease-doc-123";
            var activeLease = new Dictionary<string, object>
            {
                { "id", 101L },
                { "uid", leaseUid },
                { "tenantId", "tenant-1" },
                { "propertyId", 555L },
                { "status", "Active" },
                { "monthlyRent", 9000.0 },
                { "startDate", DateTime.UtcNow.AddMonths(-1) },
                { "endDate", DateTime.UtcNow.AddMonths(5) }
            };

            _firestore
                .Setup(x => x.QueryCollectionAsync<Dictionary<string, object>>("leases", "tenantId", "tenant-1"))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    activeLease
                });

            // ---- properties ----
            _firestore
                .Setup(x => x.GetCollectionAsync<Dictionary<string, object>>("properties"))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new() { { "id", 555L }, { "name", "Sunset Villas" }, { "address", "123 Beach Ave" } },
                    new() { { "id", 777L }, { "name", "Other" } }
                });

            // ---- invoices for tenant ----
            _firestore
                .Setup(x => x.QueryCollectionAsync<Dictionary<string, object>>("invoices", "tenantId", "tenant-1"))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "uid", "inv-1" },
                        { "invoiceNumber", "2025-001" },
                        { "amount", 9000.0 },
                        { "status", "Pending" },
                        { "dueDate", DateTime.UtcNow.AddDays(3) }
                    },
                    new()
                    {
                        { "uid", "inv-2" },
                        { "invoiceNumber", "2025-002" },
                        { "amount", 9000.0 },
                        { "status", "Paid" },
                        { "paidDate", DateTime.UtcNow.AddDays(-10) },
                        { "dueDate", DateTime.UtcNow.AddDays(-10) }
                    },
                    new()
                    {
                        { "uid", "inv-3" },
                        { "invoiceNumber", "2025-003" },
                        { "amount", 9000.0 },
                        { "status", "Overdue" },
                        { "dueDate", DateTime.UtcNow.AddDays(-2) }
                    }
                });

            // ---- maintenance for tenant ----
            _firestore
                .Setup(x => x.QueryCollectionAsync<Dictionary<string, object>>("maintenance", "reportedById", "tenant-1"))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "uid", "m1" },
                        { "title", "Light not working" },
                        { "reportedDate", DateTime.UtcNow.AddDays(-1) },
                        { "status", "Pending" },
                        { "priority", "High" }
                    },
                    new()
                    {
                        { "uid", "m2" },
                        { "title", "Door" },
                        { "reportedDate", DateTime.UtcNow.AddDays(-5) },
                        { "status", "Resolved" },
                        { "priority", "Low" }
                    }
                });

            // act
            var result = await controller.GetTenantDashboard();

            // assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var vm = Assert.IsType<TenantDashboardViewModel>(ok.Value);

            Assert.Equal("Sunset Villas", vm.PropertyName);
            Assert.Equal("123 Beach Ave", vm.PropertyAddress);
            Assert.Equal("555", vm.PropertyId);
            Assert.Equal(leaseUid, vm.LeaseId);
            Assert.Equal(9000.0, vm.MonthlyRent);
            Assert.True(vm.DaysUntilExpiry > 0);
            Assert.Equal(2, vm.UnpaidInvoices); // Pending + Overdue
            Assert.True(vm.OutstandingBalance > 0);
            Assert.Equal(1, vm.OpenMaintenanceRequests);
            Assert.Equal(1, vm.ResolvedMaintenanceRequests);
            Assert.True(vm.RecentInvoices.Count <= 5);
            Assert.True(vm.RecentMaintenanceRequests.Count <= 5);
        }
    }
}
