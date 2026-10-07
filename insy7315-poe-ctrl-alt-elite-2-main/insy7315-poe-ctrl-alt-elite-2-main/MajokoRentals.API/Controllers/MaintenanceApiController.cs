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
    public class MaintenanceController : ControllerBase
    {
        private readonly IFirestoreService _firestore;
        private readonly IUserProfileService _userProfile;
        private readonly IFileUploadService _fileUpload;
        private readonly ILogger<MaintenanceController> _logger;

        public MaintenanceController(
            IFirestoreService firestore,
            IUserProfileService userProfile,
            IFileUploadService fileUpload,
            ILogger<MaintenanceController> logger)
        {
            _firestore = firestore;
            _userProfile = userProfile;
            _fileUpload = fileUpload;
            _logger = logger;
        }

        /// <summary>
        /// Get current user's maintenance requests (tenant-specific)
        /// </summary>
        [HttpGet("my")]
        [Authorize(Roles = "Tenant")]
        [ProducesResponseType(typeof(IEnumerable<MaintenanceRequestDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyMaintenanceRequests()
        {
            // Redirect to main GetMaintenanceRequests endpoint which already filters by tenant
            return await GetMaintenanceRequests(null, null);
        }

        /// <summary>
        /// Get all maintenance requests (filtered by role)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<MaintenanceRequestDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMaintenanceRequests(
            [FromQuery] MaintenanceStatus? status = null,
            [FromQuery] MaintenancePriority? priority = null)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var allRequests = await _firestore.GetCollectionAsync<Dictionary<string, object>>("maintenance");

                // Filter by role
                var filteredRequests = allRequests.Where(m =>
                {
                    bool authorized = true;

                    // Role-based filtering
                    if (User.IsInRole("Tenant") && m.ContainsKey("reportedById"))
                    {
                        authorized = m["reportedById"].ToString() == userId;
                    }
                    else if (User.IsInRole("Caretaker") && m.ContainsKey("assignedToId"))
                    {
                        authorized = m["assignedToId"].ToString() == userId;
                    }

                    if (!authorized) return false;

                    // Status filter
                    if (status.HasValue && m.ContainsKey("status"))
                    {
                        var reqStatus = Enum.Parse<MaintenanceStatus>(m["status"].ToString()!);
                        if (reqStatus != status.Value) return false;
                    }

                    // Priority filter
                    if (priority.HasValue && m.ContainsKey("priority"))
                    {
                        var reqPriority = Enum.Parse<MaintenancePriority>(m["priority"].ToString()!);
                        if (reqPriority != priority.Value) return false;
                    }

                    return true;
                });

                var requests = filteredRequests
                    .OrderByDescending(m => m.ContainsKey("reportedDate") ? DateTime.Parse(m["reportedDate"].ToString()!) : DateTime.MinValue)
                    .Select(m => new MaintenanceRequestDto
                    {
                        Id = m.ContainsKey("uid") ? m["uid"].ToString()! : "",
                        PropertyId = m.ContainsKey("propertyId") ? Convert.ToInt64(m["propertyId"]) : 0,
                        PropertyName = m.ContainsKey("propertyName") ? m["propertyName"].ToString()! : "",
                        Title = m.ContainsKey("title") ? m["title"].ToString()! : "",
                        Description = m.ContainsKey("description") ? m["description"].ToString() : "",
                        Category = m.ContainsKey("category") ? Enum.Parse<MaintenanceCategory>(m["category"].ToString()!) : MaintenanceCategory.Other,
                        Priority = m.ContainsKey("priority") ? Enum.Parse<MaintenancePriority>(m["priority"].ToString()!) : MaintenancePriority.Medium,
                        Status = m.ContainsKey("status") ? Enum.Parse<MaintenanceStatus>(m["status"].ToString()!) : MaintenanceStatus.Pending,
                        Location = m.ContainsKey("location") ? m["location"].ToString() : "",
                        ReportedDate = m.ContainsKey("reportedDate") ? DateTime.Parse(m["reportedDate"].ToString()!) : DateTime.UtcNow,
                        ReportedByName = m.ContainsKey("reportedByName") ? m["reportedByName"].ToString() : "",
                        AssignedToName = m.ContainsKey("assignedToName") ? m["assignedToName"].ToString() : null,
                        AssignedDate = m.ContainsKey("assignedDate") ? DateTime.Parse(m["assignedDate"].ToString()!) : (DateTime?)null,
                        StartedDate = m.ContainsKey("startedDate") ? DateTime.Parse(m["startedDate"].ToString()!) : (DateTime?)null,
                        CompletedDate = m.ContainsKey("completedDate") ? DateTime.Parse(m["completedDate"].ToString()!) : (DateTime?)null,
                        EstimatedCost = m.ContainsKey("estimatedCost") ? Convert.ToDouble(m["estimatedCost"]) : (double?)null,
                        ActualCost = m.ContainsKey("actualCost") ? Convert.ToDouble(m["actualCost"]) : (double?)null,
                        ResolutionNotes = m.ContainsKey("resolutionNotes") ? m["resolutionNotes"].ToString() : null,
                        ImageUrls = m.ContainsKey("imageUrls") && m["imageUrls"] is List<object> imgs
                            ? imgs.Select(i => i.ToString()!).ToList()
                            : new List<string>(),
                        CompletionImageUrls = m.ContainsKey("completionImageUrls") && m["completionImageUrls"] is List<object> compImgs
                            ? compImgs.Select(i => i.ToString()!).ToList()
                            : new List<string>()
                    })
                    .ToList();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching maintenance requests from Firestore");
                return StatusCode(500, new { message = "Error fetching maintenance requests" });
            }
        }

        /// <summary>
        /// Get a specific maintenance request
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(MaintenanceRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMaintenanceRequest(string id)
        {
            try
            {
                var request = await _firestore.GetDocumentAsync<Dictionary<string, object>>("maintenance", id);
                if (request == null)
                    return NotFound();

                // Authorization check
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!User.IsInRole("Admin") && !User.IsInRole("PropertyManager"))
                {
                    var reportedById = request.ContainsKey("reportedById") ? request["reportedById"].ToString() : "";
                    var assignedToId = request.ContainsKey("assignedToId") ? request["assignedToId"].ToString() : "";

                    if (reportedById != userId && assignedToId != userId)
                    {
                        return Forbid();
                    }
                }

                // Get property name - fetch from property if not stored
                var propertyId = request.ContainsKey("propertyId") ? Convert.ToInt64(request["propertyId"]) : 0;
                var propertyName = request.ContainsKey("propertyName") ? request["propertyName"].ToString()! : "";

                // If property name is missing, fetch it from the property document
                if (string.IsNullOrEmpty(propertyName) && propertyId > 0)
                {
                    // Property ID is stored as timestamp, need to search for it
                    var allProperties = await _firestore.GetCollectionAsync<Dictionary<string, object>>("properties");
                    var property = allProperties.FirstOrDefault(p => p.ContainsKey("id") && Convert.ToInt64(p["id"]) == propertyId);

                    if (property != null && property.ContainsKey("name"))
                    {
                        propertyName = property["name"].ToString()!;
                    }
                }

                var dto = new MaintenanceRequestDto
                {
                    Id = id,  // Use Firestore document ID
                    PropertyId = propertyId,
                    PropertyName = propertyName,
                    Title = request.ContainsKey("title") ? request["title"].ToString()! : "",
                    Description = request.ContainsKey("description") ? request["description"].ToString() : "",
                    Category = request.ContainsKey("category") ? Enum.Parse<MaintenanceCategory>(request["category"].ToString()!) : MaintenanceCategory.Other,
                    Priority = request.ContainsKey("priority") ? Enum.Parse<MaintenancePriority>(request["priority"].ToString()!) : MaintenancePriority.Medium,
                    Status = request.ContainsKey("status") ? Enum.Parse<MaintenanceStatus>(request["status"].ToString()!) : MaintenanceStatus.Pending,
                    Location = request.ContainsKey("location") ? request["location"].ToString() : "",
                    ReportedDate = request.ContainsKey("reportedDate") ? DateTime.Parse(request["reportedDate"].ToString()!) : DateTime.UtcNow,
                    ReportedByName = request.ContainsKey("reportedByName") ? request["reportedByName"].ToString() : "",
                    AssignedToName = request.ContainsKey("assignedToName") ? request["assignedToName"].ToString() : null,
                    AssignedDate = request.ContainsKey("assignedDate") ? DateTime.Parse(request["assignedDate"].ToString()!) : (DateTime?)null,
                    StartedDate = request.ContainsKey("startedDate") ? DateTime.Parse(request["startedDate"].ToString()!) : (DateTime?)null,
                    CompletedDate = request.ContainsKey("completedDate") ? DateTime.Parse(request["completedDate"].ToString()!) : (DateTime?)null,
                    EstimatedCost = request.ContainsKey("estimatedCost") ? Convert.ToDouble(request["estimatedCost"]) : (double?)null,
                    ActualCost = request.ContainsKey("actualCost") ? Convert.ToDouble(request["actualCost"]) : (double?)null,
                    ResolutionNotes = request.ContainsKey("resolutionNotes") ? request["resolutionNotes"].ToString() : null,
                    ImageUrls = request.ContainsKey("imageUrls") && request["imageUrls"] is List<object> imgs
                        ? imgs.Select(i => i.ToString()!).ToList()
                        : new List<string>(),
                    CompletionImageUrls = request.ContainsKey("completionImageUrls") && request["completionImageUrls"] is List<object> compImgs
                        ? compImgs.Select(i => i.ToString()!).ToList()
                        : new List<string>()
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching maintenance request {id} from Firestore");
                return StatusCode(500, new { message = "Error fetching maintenance request" });
            }
        }

        /// <summary>
        /// Create a new maintenance request
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(MaintenanceRequestDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateMaintenanceRequest([FromBody] CreateMaintenanceRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userProfile.GetUserProfileAsync(userId!);

                // Get property name
                var property = await _firestore.GetDocumentAsync<Dictionary<string, object>>("properties", dto.PropertyId.ToString());
                var propertyName = property != null && property.ContainsKey("name") ? property["name"].ToString()! : "";

                var id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var requestData = new Dictionary<string, object>
                {
                    { "id", id },
                    { "propertyId", dto.PropertyId },
                    { "propertyName", propertyName },
                    { "reportedById", userId! },
                    { "reportedByName", user != null ? $"{user.FirstName} {user.LastName}" : "" },
                    { "title", dto.Title },
                    { "description", dto.Description ?? "" },
                    { "category", dto.Category.ToString() },
                    { "priority", dto.Priority.ToString() },
                    { "location", dto.Location ?? "" },
                    { "comments", dto.Comments ?? "" },
                    { "status", MaintenanceStatus.Pending.ToString() },
                    { "reportedDate", DateTime.UtcNow.ToString("o") },
                    { "imageUrls", new List<string>() },
                    { "completionImageUrls", new List<string>() }
                };

                var docId = await _firestore.AddDocumentAsync("maintenance", requestData);

                _logger.LogInformation($"Maintenance request created in Firestore with ID: {docId}");

                return CreatedAtAction(nameof(GetMaintenanceRequest), new { id = docId }, new { id = docId, requestId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating maintenance request in Firestore");
                return StatusCode(500, new { message = "Error creating maintenance request" });
            }
        }

        /// <summary>
        /// Update maintenance request status
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMaintenanceStatus(string id, [FromBody] UpdateMaintenanceStatusDto dto)
        {
            try
            {
                var request = await _firestore.GetDocumentAsync<Dictionary<string, object>>("maintenance", id);
                if (request == null)
                    return NotFound();

                // Authorization check for caretakers
                if (User.IsInRole("Caretaker"))
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var assignedToId = request.ContainsKey("assignedToId") ? request["assignedToId"].ToString() : "";
                    if (assignedToId != userId)
                        return Forbid();
                }

                request["status"] = dto.Status.ToString();
                request["updatedAt"] = DateTime.UtcNow.ToString("o");

                if (dto.Status == MaintenanceStatus.InProgress && !request.ContainsKey("startedDate"))
                {
                    request["startedDate"] = DateTime.UtcNow.ToString("o");
                }

                if (dto.Status == MaintenanceStatus.Resolved)
                {
                    request["completedDate"] = DateTime.UtcNow.ToString("o");
                    if (!string.IsNullOrEmpty(dto.Notes))
                        request["resolutionNotes"] = dto.Notes;
                    if (dto.ActualCost.HasValue)
                        request["actualCost"] = dto.ActualCost.Value;
                }

                await _firestore.UpdateDocumentAsync("maintenance", id, request);

                _logger.LogInformation($"Maintenance request {id} status updated in Firestore");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating maintenance request {id} in Firestore");
                return StatusCode(500, new { message = "Error updating maintenance request" });
            }
        }

        /// <summary>
        /// Assign a maintenance request to a caretaker
        /// </summary>
        [HttpPut("{id}/assign")]
        [Authorize(Roles = "Admin,PropertyManager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignMaintenanceRequest(string id, [FromBody] AssignMaintenanceDto dto)
        {
            try
            {
                var request = await _firestore.GetDocumentAsync<Dictionary<string, object>>("maintenance", id);
                if (request == null)
                    return NotFound();

                // Get caretaker info
                var caretaker = await _userProfile.GetUserProfileAsync(dto.AssignedToId);

                request["assignedToId"] = dto.AssignedToId;
                request["assignedToName"] = caretaker != null ? $"{caretaker.FirstName} {caretaker.LastName}" : "";
                request["assignedDate"] = DateTime.UtcNow.ToString("o");
                request["status"] = MaintenanceStatus.InProgress.ToString();
                request["updatedAt"] = DateTime.UtcNow.ToString("o");

                if (dto.EstimatedCost.HasValue)
                    request["estimatedCost"] = dto.EstimatedCost.Value;

                if (!string.IsNullOrEmpty(dto.Notes))
                    request["notes"] = dto.Notes;

                await _firestore.UpdateDocumentAsync("maintenance", id, request);

                _logger.LogInformation($"Maintenance request {id} assigned in Firestore");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning maintenance request {id} in Firestore");
                return StatusCode(500, new { message = "Error assigning maintenance request" });
            }
        }

        /// <summary>
        /// Upload post-repair completion images (for caretakers)
        /// </summary>
        [HttpPost("{id}/completion-images")]
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UploadCompletionImages(string id, [FromForm] List<IFormFile> images)
        {
            try
            {
                if (images == null || images.Count == 0)
                    return BadRequest(new { message = "No images provided" });

                // Validate all files first
                foreach (var image in images)
                {
                    if (!_fileUpload.IsValidFile(image))
                        return BadRequest(new { message = $"Invalid file: {image.FileName}" });
                }

                var request = await _firestore.GetDocumentAsync<Dictionary<string, object>>("maintenance", id);
                if (request == null)
                    return NotFound();

                // Authorization check for caretakers
                if (User.IsInRole("Caretaker"))
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var assignedToId = request.ContainsKey("assignedToId") ? request["assignedToId"].ToString() : "";
                    if (assignedToId != userId)
                        return Forbid();
                }

                // Upload images to Cloudinary
                var uploadedUrls = await _fileUpload.UploadMultipleFilesAsync(images, "maintenance/completion");

                // Get existing completion images
                var existingImages = request.ContainsKey("completionImageUrls") && request["completionImageUrls"] is List<object> imgs
                    ? imgs.Select(i => i.ToString()!).ToList()
                    : new List<string>();

                // Add new images to the list
                existingImages.AddRange(uploadedUrls);

                // Update the document
                request["completionImageUrls"] = existingImages;
                request["updatedAt"] = DateTime.UtcNow.ToString("o");

                await _firestore.UpdateDocumentAsync("maintenance", id, request);

                _logger.LogInformation($"Uploaded {uploadedUrls.Count} completion images for maintenance request {id}");

                return Ok(new { message = "Images uploaded successfully", imageUrls = uploadedUrls, totalImages = existingImages.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading completion images for maintenance request {id}");
                return StatusCode(500, new { message = "Error uploading images" });
            }
        }

        /// <summary>
        /// Add a comment/log to a maintenance request (for caretakers and managers)
        /// </summary>
        [HttpPost("{id}/comments")]
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddComment(string id, [FromBody] AddCommentDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Comment))
                    return BadRequest(new { message = "Comment cannot be empty" });

                var request = await _firestore.GetDocumentAsync<Dictionary<string, object>>("maintenance", id);
                if (request == null)
                    return NotFound();

                // Authorization check for caretakers
                if (User.IsInRole("Caretaker"))
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var assignedToId = request.ContainsKey("assignedToId") ? request["assignedToId"].ToString() : "";
                    if (assignedToId != userId)
                        return Forbid();
                }

                // Get current user info
                var userId2 = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userProfile.GetUserProfileAsync(userId2!);
                var userName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown User";

                // Get existing comments or create new list
                var existingComments = request.ContainsKey("commentHistory") && request["commentHistory"] is List<object> comments
                    ? comments.Select(c => c as Dictionary<string, object>).Where(c => c != null).ToList()
                    : new List<Dictionary<string, object>?>();

                // Create new comment object
                var newComment = new Dictionary<string, object>
                {
                    { "comment", dto.Comment },
                    { "userId", userId2! },
                    { "userName", userName },
                    { "timestamp", DateTime.UtcNow.ToString("o") },
                    { "userRole", User.IsInRole("Admin") ? "Admin" : User.IsInRole("PropertyManager") ? "PropertyManager" : "Caretaker" }
                };

                existingComments!.Add(newComment);

                // Update the document
                request["commentHistory"] = existingComments;
                request["updatedAt"] = DateTime.UtcNow.ToString("o");

                // Also update the legacy comments field for backward compatibility
                var legacyComments = request.ContainsKey("comments") ? request["comments"].ToString() : "";
                if (!string.IsNullOrEmpty(legacyComments))
                    legacyComments += "\n";
                legacyComments += $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] {userName}: {dto.Comment}";
                request["comments"] = legacyComments;

                await _firestore.UpdateDocumentAsync("maintenance", id, request);

                _logger.LogInformation($"Comment added to maintenance request {id} by user {userId2}");

                return Ok(new { message = "Comment added successfully", comment = newComment });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding comment to maintenance request {id}");
                return StatusCode(500, new { message = "Error adding comment" });
            }
        }

        /// <summary>
        /// Get comment history for a maintenance request
        /// </summary>
        [HttpGet("{id}/comments")]
        [ProducesResponseType(typeof(List<CommentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetComments(string id)
        {
            try
            {
                var request = await _firestore.GetDocumentAsync<Dictionary<string, object>>("maintenance", id);
                if (request == null)
                    return NotFound();

                // Authorization check
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!User.IsInRole("Admin") && !User.IsInRole("PropertyManager"))
                {
                    var reportedById = request.ContainsKey("reportedById") ? request["reportedById"].ToString() : "";
                    var assignedToId = request.ContainsKey("assignedToId") ? request["assignedToId"].ToString() : "";

                    if (reportedById != userId && assignedToId != userId)
                    {
                        return Forbid();
                    }
                }

                // Get comment history
                var comments = request.ContainsKey("commentHistory") && request["commentHistory"] is List<object> commentList
                    ? commentList.Select(c =>
                    {
                        var dict = c as Dictionary<string, object>;
                        if (dict == null) return null;

                        return new CommentDto
                        {
                            Comment = dict.ContainsKey("comment") ? dict["comment"].ToString()! : "",
                            UserId = dict.ContainsKey("userId") ? dict["userId"].ToString()! : "",
                            UserName = dict.ContainsKey("userName") ? dict["userName"].ToString()! : "",
                            Timestamp = dict.ContainsKey("timestamp") ? DateTime.Parse(dict["timestamp"].ToString()!) : DateTime.MinValue,
                            UserRole = dict.ContainsKey("userRole") ? dict["userRole"].ToString()! : ""
                        };
                    })
                    .Where(c => c != null)
                    .OrderBy(c => c!.Timestamp)
                    .ToList()
                    : new List<CommentDto?>();

                return Ok(comments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching comments for maintenance request {id}");
                return StatusCode(500, new { message = "Error fetching comments" });
            }
        }
    }

    // DTOs
    public class AssignMaintenanceDto
    {
        public string AssignedToId { get; set; } = string.Empty;
        public double? EstimatedCost { get; set; }
        public string? Notes { get; set; }
    }

    public class AddCommentDto
    {
        public string Comment { get; set; } = string.Empty;
    }

    public class CommentDto
    {
        public string Comment { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string UserRole { get; set; } = string.Empty;
    }
}
