using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Services;
using System.Security.Claims;

namespace MajokoRentals.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EscalationController : ControllerBase
    {
        private readonly IEscalationService _escalationService;
        private readonly ILogger<EscalationController> _logger;

        public EscalationController(
            IEscalationService escalationService,
            ILogger<EscalationController> logger)
        {
            _escalationService = escalationService;
            _logger = logger;
        }

        /// <summary>
        /// Create a new chatbot escalation
        /// </summary>
        [HttpPost("create")]
        public async Task<ActionResult<ChatbotEscalationDto>> CreateEscalation([FromBody] CreateEscalationRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var escalation = await _escalationService.CreateEscalationAsync(userId, request);
                return Ok(escalation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating escalation");
                return StatusCode(500, new { error = "Failed to create escalation", details = ex.Message });
            }
        }

        /// <summary>
        /// Get all pending escalations (Admin only)
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<EscalationsResponse>> GetPendingEscalations(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var escalations = await _escalationService.GetPendingEscalationsAsync(page, pageSize);
                return Ok(escalations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending escalations");
                return StatusCode(500, new { error = "Failed to get pending escalations", details = ex.Message });
            }
        }

        /// <summary>
        /// Get escalations assigned to the current admin
        /// </summary>
        [HttpGet("my-assigned")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<EscalationsResponse>> GetMyAssignedEscalations(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var escalations = await _escalationService.GetAdminEscalationsAsync(userId, page, pageSize);
                return Ok(escalations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting assigned escalations");
                return StatusCode(500, new { error = "Failed to get assigned escalations", details = ex.Message });
            }
        }

        /// <summary>
        /// Get escalation summary/dashboard stats
        /// </summary>
        [HttpGet("summary")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PendingEscalationsSummary>> GetSummary()
        {
            try
            {
                var summary = await _escalationService.GetEscalationsSummaryAsync();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting escalation summary");
                return StatusCode(500, new { error = "Failed to get escalation summary", details = ex.Message });
            }
        }

        /// <summary>
        /// Get admin workload statistics
        /// </summary>
        [HttpGet("workload")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<AdminWorkloadDto>>> GetAdminWorkload()
        {
            try
            {
                var workload = await _escalationService.GetAdminWorkloadAsync();
                return Ok(workload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin workload");
                return StatusCode(500, new { error = "Failed to get admin workload", details = ex.Message });
            }
        }

        /// <summary>
        /// Get escalation statistics
        /// </summary>
        [HttpGet("statistics")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<EscalationStatistics>> GetStatistics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var stats = await _escalationService.GetEscalationStatisticsAsync(startDate, endDate);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting escalation statistics");
                return StatusCode(500, new { error = "Failed to get escalation statistics", details = ex.Message });
            }
        }

        /// <summary>
        /// Assign an escalation to an admin
        /// </summary>
        [HttpPost("{escalationId}/assign")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ChatbotEscalationDto>> AssignEscalation(
            string escalationId,
            [FromBody] AssignEscalationRequest request)
        {
            try
            {
                var escalation = await _escalationService.AssignEscalationAsync(escalationId, request.AdminId);
                return Ok(escalation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning escalation {escalationId}");
                return StatusCode(500, new { error = "Failed to assign escalation", details = ex.Message });
            }
        }

        /// <summary>
        /// Assign escalation to current user (self-assign)
        /// </summary>
        [HttpPost("{escalationId}/assign-to-me")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ChatbotEscalationDto>> AssignToMe(string escalationId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var escalation = await _escalationService.AssignEscalationAsync(escalationId, userId);
                return Ok(escalation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error self-assigning escalation {escalationId}");
                return StatusCode(500, new { error = "Failed to assign escalation", details = ex.Message });
            }
        }

        /// <summary>
        /// Respond to an escalation
        /// </summary>
        [HttpPost("{escalationId}/respond")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RespondToEscalation(
            string escalationId,
            [FromBody] RespondToEscalationRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                await _escalationService.RespondToEscalationAsync(escalationId, userId, request);
                return Ok(new { success = true, message = "Response sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error responding to escalation {escalationId}");
                return StatusCode(500, new { error = "Failed to respond to escalation", details = ex.Message });
            }
        }

        /// <summary>
        /// Update escalation status
        /// </summary>
        [HttpPost("{escalationId}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ChatbotEscalationDto>> UpdateStatus(
            string escalationId,
            [FromBody] UpdateEscalationStatusRequest request)
        {
            try
            {
                var escalation = await _escalationService.UpdateEscalationStatusAsync(escalationId, request);
                return Ok(escalation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating escalation status {escalationId}");
                return StatusCode(500, new { error = "Failed to update escalation status", details = ex.Message });
            }
        }

        /// <summary>
        /// Resolve an escalation
        /// </summary>
        [HttpPost("{escalationId}/resolve")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ChatbotEscalationDto>> ResolveEscalation(
            string escalationId,
            [FromBody] ResolveEscalationRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var escalation = await _escalationService.ResolveEscalationAsync(escalationId, userId, request);
                return Ok(escalation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resolving escalation {escalationId}");
                return StatusCode(500, new { error = "Failed to resolve escalation", details = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific escalation by ID
        /// </summary>
        [HttpGet("{escalationId}")]
        public async Task<ActionResult<ChatbotEscalationDto>> GetEscalation(string escalationId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var escalation = await _escalationService.GetEscalationByIdAsync(escalationId);
                if (escalation == null)
                {
                    return NotFound(new { error = "Escalation not found" });
                }

                // Verify user has access (must be the user who created it, assigned admin, or any admin)
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                if (escalation.UserId != userId &&
                    escalation.AssignedToAdminId != userId &&
                    userRole != "Admin")
                {
                    return Forbid();
                }

                return Ok(escalation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting escalation {escalationId}");
                return StatusCode(500, new { error = "Failed to get escalation", details = ex.Message });
            }
        }
    }
}
