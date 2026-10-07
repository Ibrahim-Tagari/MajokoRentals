using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using System.Security.Claims;

namespace MajokoRentals.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LeasesController : ControllerBase
    {
        private readonly IFirestoreService _firestore;
        private readonly IUserProfileService _userProfile;
        private readonly ILogger<LeasesController> _logger;

        public LeasesController(
            IFirestoreService firestore,
            IUserProfileService userProfile,
            ILogger<LeasesController> logger)
        {
            _firestore = firestore;
            _userProfile = userProfile;
            _logger = logger;
        }

        /// <summary>
        /// Get current user's leases (tenant-specific)
        /// </summary>
        [HttpGet("my")]
        [Authorize(Roles = "Tenant")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyLeases()
        {
            // Redirect to main GetLeases endpoint which already filters by tenant
            return await GetLeases(null, null);
        }

        /// <summary>
        /// Get all leases (filtered by role)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLeases(
            [FromQuery] LeaseStatus? status = null,
            [FromQuery] bool? expiringSoon = null)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var allLeases = await _firestore.GetCollectionAsync<Dictionary<string, object>>("leases");

                // Filter by role
                var filteredLeases = allLeases.Where(l =>
                {
                    // Tenant filtering
                    if (User.IsInRole("Tenant") && l.ContainsKey("tenantId"))
                    {
                        if (l["tenantId"].ToString() != userId) return false;
                    }

                    // Status filter
                    if (status.HasValue && l.ContainsKey("status"))
                    {
                        var leaseStatus = Enum.Parse<LeaseStatus>(l["status"].ToString()!);
                        if (leaseStatus != status.Value) return false;
                    }

                    // Expiring soon filter
                    if (expiringSoon.HasValue && expiringSoon.Value && l.ContainsKey("endDate") && l.ContainsKey("status"))
                    {
                        var endDate = DateTime.Parse(l["endDate"].ToString()!);
                        var leaseStatus = Enum.Parse<LeaseStatus>(l["status"].ToString()!);
                        var daysUntilExpiry = (endDate - DateTime.UtcNow).Days;
                        if (!(daysUntilExpiry <= 30 && leaseStatus == LeaseStatus.Active)) return false;
                    }

                    return true;
                });

                var leases = filteredLeases
                    .OrderByDescending(l => l.ContainsKey("startDate") ? DateTime.Parse(l["startDate"].ToString()!) : DateTime.MinValue)
                    .Select(l =>
                    {
                        var endDate = l.ContainsKey("endDate") ? DateTime.Parse(l["endDate"].ToString()!) : DateTime.UtcNow;
                        var daysUntilExpiry = (endDate - DateTime.UtcNow).Days;

                        return new
                        {
                            id = l.ContainsKey("uid") ? l["uid"].ToString() : "",  // Firestore document ID for lookups
                            leaseNumber = l.ContainsKey("id") ? Convert.ToInt64(l["id"]) : 0L,  // Original timestamp ID for display
                            propertyId = l.ContainsKey("propertyId") ? Convert.ToInt64(l["propertyId"]) : 0,
                            propertyName = l.ContainsKey("propertyName") ? l["propertyName"].ToString() : "",
                            propertyAddress = l.ContainsKey("propertyAddress") ? l["propertyAddress"].ToString() : "",
                            tenantId = l.ContainsKey("tenantId") ? l["tenantId"].ToString() : "",
                            tenantName = l.ContainsKey("tenantName") ? l["tenantName"].ToString() : "",
                            tenantEmail = l.ContainsKey("tenantEmail") ? l["tenantEmail"].ToString() : "",
                            startDate = l.ContainsKey("startDate") ? DateTime.Parse(l["startDate"].ToString()!) : DateTime.UtcNow,
                            endDate = endDate,
                            monthlyRent = l.ContainsKey("monthlyRent") ? Convert.ToDouble(l["monthlyRent"]) : 0,
                            depositAmount = l.ContainsKey("depositAmount") ? Convert.ToDouble(l["depositAmount"]) : 0,
                            status = l.ContainsKey("status") ? Enum.Parse<LeaseStatus>(l["status"].ToString()!) : LeaseStatus.Draft,
                            daysUntilExpiry = daysUntilExpiry,
                            isExpiringSoon = daysUntilExpiry <= 30 && daysUntilExpiry > 0,
                            isExpired = endDate < DateTime.UtcNow
                        };
                    })
                    .ToList();

                return Ok(leases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching leases from Firestore");
                return StatusCode(500, new { message = "Error fetching leases" });
            }
        }

        /// <summary>
        /// Get a specific lease
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLease(string id)
        {
            try
            {
                var lease = await _firestore.GetDocumentAsync<Dictionary<string, object>>("leases", id);
                if (lease == null)
                    return NotFound();

                // Authorization check
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!User.IsInRole("Admin") && !User.IsInRole("PropertyManager"))
                {
                    var tenantId = lease.ContainsKey("tenantId") ? lease["tenantId"].ToString() : "";
                    if (tenantId != userId)
                        return Forbid();
                }

                // Parse dates and calculate expiry information
                var endDate = lease.ContainsKey("endDate") ? DateTime.Parse(lease["endDate"].ToString()!) : DateTime.UtcNow;
                var daysUntilExpiry = (endDate - DateTime.UtcNow).Days;

                // Transform the response to use Firestore document ID as the id field
                var response = new Dictionary<string, object>(lease)
                {
                    ["id"] = id,  // Firestore document ID for lookups
                    ["leaseNumber"] = lease.ContainsKey("id") ? lease["id"] : 0L,  // Original timestamp ID for display
                    ["daysUntilExpiry"] = daysUntilExpiry,
                    ["isExpiringSoon"] = daysUntilExpiry <= 30 && daysUntilExpiry > 0,
                    ["isExpired"] = endDate < DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching lease {id} from Firestore");
                return StatusCode(500, new { message = "Error fetching lease" });
            }
        }

        /// <summary>
        /// Create a new lease
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,PropertyManager")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateLease([FromBody] CreateLeaseDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Get property details
                var allProperties = await _firestore.GetCollectionAsync<Dictionary<string, object>>("properties");
                var property = allProperties.FirstOrDefault(p => p.ContainsKey("id") && Convert.ToInt64(p["id"]) == dto.PropertyId);

                // Get tenant details
                var tenant = await _userProfile.GetUserProfileAsync(dto.TenantId);

                var leaseData = new Dictionary<string, object>
                {
                    { "id", id },
                    { "propertyId", dto.PropertyId },
                    { "propertyName", property != null && property.ContainsKey("name") ? property["name"].ToString()! : "" },
                    { "propertyAddress", property != null && property.ContainsKey("address") ? property["address"].ToString()! : "" },
                    { "tenantId", dto.TenantId },
                    { "tenantName", tenant != null ? $"{tenant.FirstName} {tenant.LastName}" : "" },
                    { "tenantEmail", tenant?.Email ?? "" },
                    { "tenantPhone", tenant?.PhoneNumber ?? "" },
                    { "startDate", dto.StartDate.ToString("o") },
                    { "endDate", dto.EndDate.ToString("o") },
                    { "monthlyRent", (double)dto.MonthlyRent },
                    { "depositAmount", (double)dto.DepositAmount },
                    { "paymentDueDay", dto.PaymentDueDay ?? 1 },
                    { "status", LeaseStatus.Active.ToString() },
                    { "terms", dto.Terms ?? "" },
                    { "specialConditions", dto.SpecialConditions ?? "" },
                    { "createdAt", DateTime.UtcNow.ToString("o") },
                    { "updatedAt", DateTime.UtcNow.ToString("o") }
                };

                var docId = await _firestore.AddDocumentAsync("leases", leaseData);

                // Update property status to Occupied
                if (property != null)
                {
                    var propertyDocId = allProperties.IndexOf(property).ToString();
                    property["status"] = PropertyStatus.Occupied.ToString();
                    property["updatedAt"] = DateTime.UtcNow.ToString("o");
                    // Note: In production, you'd properly track document IDs
                    await _firestore.UpdateDocumentAsync("properties", docId, property);
                }

                _logger.LogInformation($"Lease created in Firestore with ID: {docId}");

                return CreatedAtAction(nameof(GetLease), new { id = docId }, new { id = docId, leaseId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating lease in Firestore");
                return StatusCode(500, new { message = "Error creating lease" });
            }
        }

        /// <summary>
        /// Update a lease
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,PropertyManager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateLease(string id, [FromBody] UpdateLeaseDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var lease = await _firestore.GetDocumentAsync<Dictionary<string, object>>("leases", id);
                if (lease == null)
                    return NotFound();

                // Get property details if property changed
                var allProperties = await _firestore.GetCollectionAsync<Dictionary<string, object>>("properties");
                var property = allProperties.FirstOrDefault(p => p.ContainsKey("id") && Convert.ToInt64(p["id"]) == dto.PropertyId);

                // Get tenant details if tenant changed
                var tenant = await _userProfile.GetUserProfileAsync(dto.TenantId);

                // Update lease data
                lease["propertyId"] = dto.PropertyId;
                lease["propertyName"] = property != null && property.ContainsKey("name") ? property["name"].ToString()! : "";
                lease["propertyAddress"] = property != null && property.ContainsKey("address") ? property["address"].ToString()! : "";
                lease["tenantId"] = dto.TenantId;
                lease["tenantName"] = tenant != null ? $"{tenant.FirstName} {tenant.LastName}" : "";
                lease["tenantEmail"] = tenant?.Email ?? "";
                lease["tenantPhone"] = tenant?.PhoneNumber ?? "";
                lease["startDate"] = dto.StartDate.ToString("o");
                lease["endDate"] = dto.EndDate.ToString("o");
                lease["monthlyRent"] = (double)dto.MonthlyRent;
                lease["depositAmount"] = (double)dto.DepositAmount;
                lease["status"] = dto.Status.ToString();
                lease["updatedAt"] = DateTime.UtcNow.ToString("o");

                // Update lease document URL if provided
                if (!string.IsNullOrEmpty(dto.LeaseDocumentUrl))
                {
                    lease["leaseDocumentUrl"] = dto.LeaseDocumentUrl;
                }

                await _firestore.UpdateDocumentAsync("leases", id, lease);

                _logger.LogInformation($"Lease {id} updated successfully");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating lease {id} in Firestore");
                return StatusCode(500, new { message = "Error updating lease" });
            }
        }

        /// <summary>
        /// Renew a lease
        /// </summary>
        [HttpPost("{id}/renew")]
        [Authorize(Roles = "Admin,PropertyManager")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RenewLease(string id, [FromBody] RenewLeaseDto dto)
        {
            try
            {
                var oldLease = await _firestore.GetDocumentAsync<Dictionary<string, object>>("leases", id);
                if (oldLease == null)
                    return NotFound();

                // Update old lease status
                oldLease["status"] = LeaseStatus.Expired.ToString();
                oldLease["updatedAt"] = DateTime.UtcNow.ToString("o");
                await _firestore.UpdateDocumentAsync("leases", id, oldLease);

                // Create new lease
                var newId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var newLeaseData = new Dictionary<string, object>
                {
                    { "id", newId },
                    { "propertyId", oldLease["propertyId"] },
                    { "propertyName", oldLease["propertyName"] },
                    { "propertyAddress", oldLease["propertyAddress"] },
                    { "tenantId", oldLease["tenantId"] },
                    { "tenantName", oldLease["tenantName"] },
                    { "tenantEmail", oldLease["tenantEmail"] },
                    { "tenantPhone", oldLease.ContainsKey("tenantPhone") ? oldLease["tenantPhone"] : "" },
                    { "startDate", dto.NewStartDate.ToString("o") },
                    { "endDate", dto.NewEndDate.ToString("o") },
                    { "monthlyRent", (double)dto.NewMonthlyRent },
                    { "depositAmount", oldLease.ContainsKey("depositAmount") ? oldLease["depositAmount"] : 0 },
                    { "paymentDueDay", oldLease.ContainsKey("paymentDueDay") ? oldLease["paymentDueDay"] : 1 },
                    { "status", LeaseStatus.Active.ToString() },
                    { "terms", oldLease.ContainsKey("terms") ? oldLease["terms"] : "" },
                    { "specialConditions", oldLease.ContainsKey("specialConditions") ? oldLease["specialConditions"] : "" },
                    { "renewedFromLeaseId", oldLease["id"] },
                    { "createdAt", DateTime.UtcNow.ToString("o") },
                    { "updatedAt", DateTime.UtcNow.ToString("o") }
                };

                var newDocId = await _firestore.AddDocumentAsync("leases", newLeaseData);

                _logger.LogInformation($"Lease renewed. Old: {id}, New: {newDocId}");

                return CreatedAtAction(nameof(GetLease), new { id = newDocId }, new { id = newDocId, leaseId = newId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error renewing lease {id} in Firestore");
                return StatusCode(500, new { message = "Error renewing lease" });
            }
        }

        /// <summary>
        /// Terminate a lease
        /// </summary>
        [HttpPost("{id}/terminate")]
        [Authorize(Roles = "Admin,PropertyManager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> TerminateLease(string id, [FromBody] TerminateLeaseDto dto)
        {
            try
            {
                var lease = await _firestore.GetDocumentAsync<Dictionary<string, object>>("leases", id);
                if (lease == null)
                    return NotFound();

                lease["status"] = LeaseStatus.Terminated.ToString();
                lease["terminationDate"] = dto.TerminationDate.ToString("o");
                lease["terminationReason"] = dto.Reason ?? "";
                lease["updatedAt"] = DateTime.UtcNow.ToString("o");

                await _firestore.UpdateDocumentAsync("leases", id, lease);

                // Update property status to Available
                var propertyId = lease.ContainsKey("propertyId") ? Convert.ToInt64(lease["propertyId"]) : 0;
                var allProperties = await _firestore.GetCollectionAsync<Dictionary<string, object>>("properties");
                var property = allProperties.FirstOrDefault(p => p.ContainsKey("id") && Convert.ToInt64(p["id"]) == propertyId);

                if (property != null)
                {
                    property["status"] = PropertyStatus.Available.ToString();
                    property["updatedAt"] = DateTime.UtcNow.ToString("o");
                    // Note: Proper document ID tracking needed in production
                    var propertyDocId = allProperties.IndexOf(property).ToString();
                    await _firestore.UpdateDocumentAsync("properties", propertyDocId, property);
                }

                _logger.LogInformation($"Lease {id} terminated");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error terminating lease {id} in Firestore");
                return StatusCode(500, new { message = "Error terminating lease" });
            }
        }
    }

    // DTOs for Leases
    public class CreateLeaseDto
    {
        public long PropertyId { get; set; }
        public string TenantId { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal MonthlyRent { get; set; }
        public decimal DepositAmount { get; set; }
        public int? PaymentDueDay { get; set; }
        public string? Terms { get; set; }
        public string? SpecialConditions { get; set; }
    }

    public class UpdateLeaseDto
    {
        public long PropertyId { get; set; }
        public string TenantId { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal MonthlyRent { get; set; }
        public decimal DepositAmount { get; set; }
        public string? LeaseDocumentUrl { get; set; }
        public LeaseStatus Status { get; set; }
    }

    public class RenewLeaseDto
    {
        public DateTime NewStartDate { get; set; }
        public DateTime NewEndDate { get; set; }
        public decimal NewMonthlyRent { get; set; }
    }

    public class TerminateLeaseDto
    {
        public DateTime TerminationDate { get; set; }
        public string? Reason { get; set; }
    }
}
