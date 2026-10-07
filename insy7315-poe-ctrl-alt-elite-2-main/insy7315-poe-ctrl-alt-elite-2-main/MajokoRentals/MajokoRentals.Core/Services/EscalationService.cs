using Google.Cloud.Firestore;
using MajokoRentals.Core.Data;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Models.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MajokoRentals.Core.Services
{
    /// <summary>
    /// Firestore-based escalation service for chatbot escalations to admins
    /// </summary>
    public class EscalationService : IEscalationService
    {
        private readonly IFirestoreService _firestoreService;
        private readonly ApplicationDbContext _dbContext;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly ILogger<EscalationService> _logger;

        private const string EscalationsCollection = "chatbot-escalations";

        public EscalationService(
            IFirestoreService firestoreService,
            ApplicationDbContext dbContext,
            IEmailService emailService,
            IEmailTemplateService emailTemplateService,
            ILogger<EscalationService> logger)
        {
            _firestoreService = firestoreService;
            _dbContext = dbContext;
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
            _logger = logger;
        }

        public async Task<ChatbotEscalationDto> CreateEscalationAsync(string userId, CreateEscalationRequest request)
        {
            try
            {
                // Get user details from Firestore
                var user = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>("users", userId);

                // Get conversation messages from SQL database
                var conversation = await _dbContext.ChatConversations
                    .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(10))
                    .FirstOrDefaultAsync(c => c.Id == request.ChatConversationId);

                if (conversation == null)
                {
                    throw new Exception($"Chat conversation {request.ChatConversationId} not found");
                }

                // Prepare recent messages snapshot
                var recentMessages = conversation.Messages
                    .OrderByDescending(m => m.SentAt)
                    .Take(10)
                    .Select(m => new ChatMessageSnapshot
                    {
                        Id = m.Id,
                        Message = m.Message,
                        IsFromUser = m.IsFromUser,
                        SentAt = Timestamp.FromDateTime(m.SentAt.ToUniversalTime()),
                        Intent = m.Intent,
                        ConfidenceScore = m.ConfidenceScore
                    })
                    .Reverse()
                    .ToList();

                // Create escalation document
                string userName = "Unknown User";
                if (user != null)
                {
                    var firstName = user.ContainsKey("firstName") ? user["firstName"]?.ToString() : "";
                    var lastName = user.ContainsKey("lastName") ? user["lastName"]?.ToString() : "";
                    userName = $"{firstName} {lastName}".Trim();
                    if (string.IsNullOrWhiteSpace(userName))
                    {
                        userName = user.ContainsKey("email") ? user["email"]?.ToString() ?? "Unknown User" : "Unknown User";
                    }
                }

                var escalation = new ChatbotEscalationDocument
                {
                    ChatConversationId = request.ChatConversationId,
                    UserId = userId,
                    UserName = userName,
                    UserEmail = user?.ContainsKey("email") == true ? user["email"]?.ToString() : null,
                    UserPhone = user?.ContainsKey("phoneNumber") == true ? user["phoneNumber"]?.ToString() : null,
                    UserProfilePictureUrl = user?.ContainsKey("profileImageUrl") == true ? user["profileImageUrl"]?.ToString() : null,
                    UserRole = user?.ContainsKey("role") == true ? user["role"]?.ToString() ?? "Tenant" : "Tenant",
                    Status = "Pending",
                    Priority = request.Priority.ToString(),
                    EscalationReason = request.EscalationReason,
                    Category = request.Category ?? "General Support",
                    IssueSummary = request.IssueSummary,
                    Tags = request.Tags,
                    RecentMessages = recentMessages,
                    EmailNotificationSent = false,
                    InAppNotificationCreated = false,
                    CreatedAt = Timestamp.GetCurrentTimestamp(),
                    UpdatedAt = Timestamp.GetCurrentTimestamp()
                };

                // Save to Firestore
                var docRef = await _firestoreService.AddDocumentAsync(EscalationsCollection, escalation);
                escalation.Id = docRef;

                // Update SQL conversation as escalated
                conversation.IsEscalated = true;
                conversation.EscalatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Escalation created: {escalation.Id} for user {userId}");

                var escalationDto = MapToDto(escalation);

                // Send notifications to all admins
                await NotifyAdminsOfEscalationAsync(escalationDto);

                return escalationDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating escalation for user {userId}");
                throw;
            }
        }

        public async Task<ChatbotEscalationDto> AssignEscalationAsync(string escalationId, string adminId)
        {
            try
            {
                var escalation = await _firestoreService.GetDocumentAsync<ChatbotEscalationDocument>(EscalationsCollection, escalationId);
                if (escalation == null)
                {
                    throw new Exception($"Escalation {escalationId} not found");
                }

                // Get admin details
                var admin = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>("users", adminId);

                string adminName = "Unknown Admin";
                if (admin != null)
                {
                    var firstName = admin.ContainsKey("firstName") ? admin["firstName"]?.ToString() : "";
                    var lastName = admin.ContainsKey("lastName") ? admin["lastName"]?.ToString() : "";
                    adminName = $"{firstName} {lastName}".Trim();
                    if (string.IsNullOrWhiteSpace(adminName))
                    {
                        adminName = admin.ContainsKey("email") ? admin["email"]?.ToString() ?? "Unknown Admin" : "Unknown Admin";
                    }
                }

                var updates = new Dictionary<string, object>
                {
                    { "Status", "Assigned" },
                    { "AssignedToAdminId", adminId },
                    { "AssignedToAdminName", adminName },
                    { "AssignedAt", Timestamp.GetCurrentTimestamp() },
                    { "UpdatedAt", Timestamp.GetCurrentTimestamp() }
                };

                await _firestoreService.UpdateDocumentAsync(EscalationsCollection, escalationId, updates);

                // Get updated escalation
                escalation = await _firestoreService.GetDocumentAsync<ChatbotEscalationDocument>(EscalationsCollection, escalationId);

                _logger.LogInformation($"Escalation {escalationId} assigned to admin {adminId}");

                var escalationDto = MapToDto(escalation!);

                // Notify assigned admin
                await NotifyAdminOfAssignmentAsync(escalationDto);

                return escalationDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning escalation {escalationId}");
                throw;
            }
        }

        public async Task<ChatbotEscalationDto> UpdateEscalationStatusAsync(string escalationId, UpdateEscalationStatusRequest request)
        {
            try
            {
                var updates = new Dictionary<string, object>
                {
                    { "Status", request.Status.ToString() },
                    { "UpdatedAt", Timestamp.GetCurrentTimestamp() }
                };

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    updates["ResolutionNotes"] = request.Notes;
                }

                await _firestoreService.UpdateDocumentAsync(EscalationsCollection, escalationId, updates);

                var escalation = await _firestoreService.GetDocumentAsync<ChatbotEscalationDocument>(EscalationsCollection, escalationId);

                _logger.LogInformation($"Escalation {escalationId} status updated to {request.Status}");

                return MapToDto(escalation!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating escalation status {escalationId}");
                throw;
            }
        }

        public async Task<ChatbotEscalationDto> ResolveEscalationAsync(string escalationId, string adminId, ResolveEscalationRequest request)
        {
            try
            {
                var escalation = await _firestoreService.GetDocumentAsync<ChatbotEscalationDocument>(EscalationsCollection, escalationId);
                if (escalation == null)
                {
                    throw new Exception($"Escalation {escalationId} not found");
                }

                var now = Timestamp.GetCurrentTimestamp();
                var resolutionTimeHours = (now.ToDateTime() - escalation.CreatedAt.ToDateTime()).TotalHours;

                var updates = new Dictionary<string, object>
                {
                    { "Status", "Resolved" },
                    { "ResolvedAt", now },
                    { "ResolutionNotes", request.ResolutionNotes },
                    { "ResolutionTimeHours", resolutionTimeHours },
                    { "UpdatedAt", now }
                };

                await _firestoreService.UpdateDocumentAsync(EscalationsCollection, escalationId, updates);

                // Get updated escalation
                escalation = await _firestoreService.GetDocumentAsync<ChatbotEscalationDocument>(EscalationsCollection, escalationId);

                _logger.LogInformation($"Escalation {escalationId} resolved by admin {adminId}");

                var escalationDto = MapToDto(escalation!);

                // Notify user of resolution
                await NotifyUserOfResolutionAsync(escalationDto);

                return escalationDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resolving escalation {escalationId}");
                throw;
            }
        }

        public async Task<ChatbotEscalationDto?> GetEscalationByIdAsync(string escalationId)
        {
            try
            {
                var escalation = await _firestoreService.GetDocumentAsync<ChatbotEscalationDocument>(EscalationsCollection, escalationId);
                return escalation != null ? MapToDto(escalation) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting escalation {escalationId}");
                throw;
            }
        }

        public async Task<EscalationsResponse> GetPendingEscalationsAsync(int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                // Note: Firestore doesn't support WhereIn with multiple values in a simple query,
                // so we'll query each status separately and combine results
                var pendingEscalations = await _firestoreService.QueryCollectionAsync<ChatbotEscalationDocument>(
                    EscalationsCollection, "Status", "Pending");
                var assignedEscalations = await _firestoreService.QueryCollectionAsync<ChatbotEscalationDocument>(
                    EscalationsCollection, "Status", "Assigned");
                var inProgressEscalations = await _firestoreService.QueryCollectionAsync<ChatbotEscalationDocument>(
                    EscalationsCollection, "Status", "InProgress");

                var allEscalations = pendingEscalations
                    .Concat(assignedEscalations)
                    .Concat(inProgressEscalations)
                    .OrderByDescending(e => e.Priority)
                    .ThenByDescending(e => e.CreatedAt)
                    .ToList();

                var totalCount = allEscalations.Count;
                var paginatedEscalations = allEscalations
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(MapToDto)
                    .ToList();

                return new EscalationsResponse
                {
                    Escalations = paginatedEscalations,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    HasMore = (pageNumber * pageSize) < totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending escalations");
                throw;
            }
        }

        public async Task<EscalationsResponse> GetAdminEscalationsAsync(string adminId, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                // Get all escalations assigned to this admin
                var allEscalations = await _firestoreService.QueryCollectionAsync<ChatbotEscalationDocument>(
                    EscalationsCollection,
                    "AssignedToAdminId",
                    adminId
                );

                // Filter by status and sort
                var filteredEscalations = allEscalations
                    .Where(e => e.Status == "Assigned" || e.Status == "InProgress" || e.Status == "AwaitingUserResponse")
                    .OrderByDescending(e => e.CreatedAt)
                    .ToList();

                var totalCount = filteredEscalations.Count;
                var paginatedEscalations = filteredEscalations
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(MapToDto)
                    .ToList();

                return new EscalationsResponse
                {
                    Escalations = paginatedEscalations,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    HasMore = (pageNumber * pageSize) < totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting escalations for admin {adminId}");
                throw;
            }
        }

        public async Task<PendingEscalationsSummary> GetEscalationsSummaryAsync()
        {
            try
            {
                // Get pending escalations
                var pendingEscalations = await _firestoreService.QueryCollectionAsync<ChatbotEscalationDocument>(
                    EscalationsCollection, "Status", "Pending");

                // Get assigned escalations
                var assignedEscalations = await _firestoreService.QueryCollectionAsync<ChatbotEscalationDocument>(
                    EscalationsCollection, "Status", "Assigned");

                // Get in-progress escalations
                var inProgressEscalations = await _firestoreService.QueryCollectionAsync<ChatbotEscalationDocument>(
                    EscalationsCollection, "Status", "InProgress");

                // Get recent pending escalations (top 10)
                var recentEscalations = pendingEscalations
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(10)
                    .Select(MapToDto)
                    .ToList();

                // Get all active escalations for priority counts
                var activeEscalations = pendingEscalations
                    .Concat(assignedEscalations)
                    .Concat(inProgressEscalations)
                    .ToList();

                var urgentCount = activeEscalations.Count(e => e.Priority == "Urgent");
                var highPriorityCount = activeEscalations.Count(e => e.Priority == "High");

                // Calculate average response time from resolved escalations
                var resolvedEscalations = await _firestoreService.QueryCollectionAsync<ChatbotEscalationDocument>(
                    EscalationsCollection, "Status", "Resolved");

                var escalationsWithResponseTime = resolvedEscalations
                    .Where(e => e.ResponseTimeMinutes.HasValue)
                    .ToList();

                var avgResponseTime = escalationsWithResponseTime.Any()
                    ? escalationsWithResponseTime.Average(e => e.ResponseTimeMinutes!.Value)
                    : 0;

                return new PendingEscalationsSummary
                {
                    TotalPending = pendingEscalations.Count,
                    TotalAssigned = assignedEscalations.Count,
                    TotalInProgress = inProgressEscalations.Count,
                    UrgentCount = urgentCount,
                    HighPriorityCount = highPriorityCount,
                    AverageResponseTimeMinutes = avgResponseTime,
                    RecentEscalations = recentEscalations
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting escalations summary");
                throw;
            }
        }

        public async Task<List<AdminWorkloadDto>> GetAdminWorkloadAsync()
        {
            try
            {
                // Get all admins from Firestore users collection
                var admins = await _firestoreService.QueryCollectionAsync<Dictionary<string, object>>(
                    "users", "role", "Admin");

                var workloads = new List<AdminWorkloadDto>();

                // Get all escalations once to reduce queries
                var allEscalations = await _firestoreService.GetCollectionAsync<ChatbotEscalationDocument>(
                    EscalationsCollection);

                foreach (var adminData in admins)
                {
                    var adminId = adminData.ContainsKey("Id") ? adminData["Id"]?.ToString() : null;
                    if (string.IsNullOrEmpty(adminId)) continue;

                    // Get assigned escalations for this admin
                    var assignedEscalations = allEscalations
                        .Where(e => e.AssignedToAdminId == adminId)
                        .ToList();

                    var pendingCount = assignedEscalations.Count(e => e.Status == "Pending");
                    var inProgressCount = assignedEscalations.Count(e => e.Status == "InProgress");

                    // Get resolved today
                    var today = DateTime.UtcNow.Date;
                    var resolvedTodayCount = assignedEscalations
                        .Count(e => e.Status == "Resolved" &&
                               e.ResolvedAt.HasValue &&
                               e.ResolvedAt.Value.ToDateTime().Date == today);

                    // Calculate average resolution time
                    var resolvedEscalations = assignedEscalations
                        .Where(e => e.Status == "Resolved" && e.ResolutionTimeHours.HasValue)
                        .ToList();

                    var avgResolutionTime = resolvedEscalations.Any()
                        ? resolvedEscalations.Average(e => e.ResolutionTimeHours!.Value)
                        : 0;

                    string adminName = "Unknown";
                    if (adminData != null)
                    {
                        var firstName = adminData.ContainsKey("firstName") ? adminData["firstName"]?.ToString() : "";
                        var lastName = adminData.ContainsKey("lastName") ? adminData["lastName"]?.ToString() : "";
                        adminName = $"{firstName} {lastName}".Trim();
                        if (string.IsNullOrWhiteSpace(adminName))
                        {
                            adminName = adminData.ContainsKey("email") ? adminData["email"]?.ToString() ?? "Unknown" : "Unknown";
                        }
                    }

                    workloads.Add(new AdminWorkloadDto
                    {
                        AdminId = adminId,
                        AdminName = adminName,
                        AssignedEscalations = assignedEscalations.Count,
                        PendingEscalations = pendingCount,
                        InProgressEscalations = inProgressCount,
                        ResolvedToday = resolvedTodayCount,
                        AverageResolutionTimeHours = avgResolutionTime
                    });
                }

                return workloads.OrderByDescending(w => w.AssignedEscalations).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin workload");
                throw;
            }
        }

        public async Task RespondToEscalationAsync(string escalationId, string adminId, RespondToEscalationRequest request)
        {
            try
            {
                var escalation = await _firestoreService.GetDocumentAsync<ChatbotEscalationDocument>(EscalationsCollection, escalationId);
                if (escalation == null)
                {
                    throw new Exception($"Escalation {escalationId} not found");
                }

                var now = Timestamp.GetCurrentTimestamp();
                var updates = new Dictionary<string, object>
                {
                    { "UpdatedAt", now }
                };

                // Set first response time if not already set
                if (!escalation.FirstResponseAt.HasValue)
                {
                    var responseTimeMinutes = (int)(now.ToDateTime() - escalation.CreatedAt.ToDateTime()).TotalMinutes;
                    updates["FirstResponseAt"] = now;
                    updates["ResponseTimeMinutes"] = responseTimeMinutes;
                }

                // Update status if provided
                if (request.NewStatus.HasValue)
                {
                    updates["Status"] = request.NewStatus.Value.ToString();
                }

                await _firestoreService.UpdateDocumentAsync(EscalationsCollection, escalationId, updates);

                // Get updated escalation
                escalation = await _firestoreService.GetDocumentAsync<ChatbotEscalationDocument>(EscalationsCollection, escalationId);

                _logger.LogInformation($"Admin {adminId} responded to escalation {escalationId}");

                // Notify user of response
                await NotifyUserOfResponseAsync(MapToDto(escalation!), request.Response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error responding to escalation {escalationId}");
                throw;
            }
        }

        public async Task NotifyAdminsOfEscalationAsync(ChatbotEscalationDto escalation)
        {
            try
            {
                // Get all admins from Firestore
                var admins = await _firestoreService.QueryCollectionAsync<Dictionary<string, object>>(
                    "users", "role", "Admin");

                var subject = $"New Chatbot Escalation - {escalation.Priority} Priority";

                var conversationHistory = escalation.RecentMessages != null && escalation.RecentMessages.Any()
                    ? string.Join("<br>", escalation.RecentMessages.Select(m =>
                        $"<div style='margin: 10px 0;'><strong>{(m.IsFromUser ? escalation.UserName : "Bot")}:</strong> {m.Message}</div>"))
                    : "No conversation history available.";

                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2 style='color: #d32f2f;'>New Chatbot Escalation</h2>
                        <div style='background: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p><strong>Escalation ID:</strong> {escalation.Id}</p>
                            <p><strong>Priority:</strong> <span style='color: {(escalation.Priority == EscalationPriority.Urgent ? "#d32f2f" : "#ff9800")};'>{escalation.Priority}</span></p>
                            <p><strong>User:</strong> {escalation.UserName} ({escalation.UserRole})</p>
                            <p><strong>Email:</strong> {escalation.UserEmail}</p>
                            <p><strong>Category:</strong> {escalation.Category ?? "General Support"}</p>
                            <p><strong>Issue Summary:</strong> {escalation.IssueSummary ?? "User needs assistance"}</p>
                        </div>
                        <h3>Conversation History:</h3>
                        <div style='background: #fff; padding: 15px; border: 1px solid #ddd; border-radius: 5px;'>
                            {conversationHistory}
                        </div>
                        <p style='margin-top: 30px;'>
                            <a href='#' style='background: #1976d2; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>
                                View Escalation
                            </a>
                        </p>
                        <hr style='margin: 30px 0;'>
                        <p style='color: #666; font-size: 12px;'>
                            This is an automated notification from Majoko Rentals. Please respond as soon as possible.
                        </p>
                    </body>
                    </html>
                ";

                // Send email to all admins
                var emailTasks = new List<Task>();
                foreach (var adminData in admins)
                {
                    var adminEmail = adminData.ContainsKey("email") ? adminData["email"]?.ToString() : null;
                    if (!string.IsNullOrEmpty(adminEmail))
                    {
                        emailTasks.Add(_emailService.SendEmailAsync(adminEmail, subject, body, true));
                    }
                }

                await Task.WhenAll(emailTasks);

                // Update escalation to mark notification as sent
                var updates = new Dictionary<string, object>
                {
                    { "EmailNotificationSent", true },
                    { "EmailNotificationSentAt", Timestamp.GetCurrentTimestamp() },
                    { "InAppNotificationCreated", true }
                };

                await _firestoreService.UpdateDocumentAsync(EscalationsCollection, escalation.Id.ToString(), updates);

                _logger.LogInformation($"Escalation notifications sent to {emailTasks.Count} admins");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending admin notifications");
            }
        }

        public async Task NotifyUserOfResponseAsync(ChatbotEscalationDto escalation, string response)
        {
            try
            {
                var subject = $"Response to Your Support Request";
                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>We've Responded to Your Request</h2>
                        <p>Hi {escalation.UserName},</p>
                        <p>Our support team has reviewed your request and provided a response:</p>
                        <div style='background: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p>{response}</p>
                        </div>
                        <p>If you need further assistance, please reply to this email or continue the conversation in your account.</p>
                        <p>Best regards,<br>Majoko Rentals Support Team</p>
                    </body>
                    </html>
                ";

                if (!string.IsNullOrEmpty(escalation.UserEmail))
                {
                    await _emailService.SendEmailAsync(escalation.UserEmail, subject, body, true);
                }

                _logger.LogInformation($"Response notification sent to user {escalation.UserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending user response notification");
            }
        }

        public async Task NotifyAdminOfAssignmentAsync(ChatbotEscalationDto escalation)
        {
            try
            {
                // Get admin email from Firestore
                var admin = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>("users", escalation.AssignedToAdminId!);
                var adminEmail = admin?["email"]?.ToString();

                if (string.IsNullOrEmpty(adminEmail))
                {
                    _logger.LogWarning($"Could not find email for admin {escalation.AssignedToAdminId}");
                    return;
                }

                var subject = $"Escalation Assigned to You - {escalation.Category}";
                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>New Escalation Assigned</h2>
                        <p>Hi {escalation.AssignedToAdminName},</p>
                        <p>An escalation has been assigned to you:</p>
                        <div style='background: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p><strong>User:</strong> {escalation.UserName}</p>
                            <p><strong>Priority:</strong> {escalation.Priority}</p>
                            <p><strong>Category:</strong> {escalation.Category}</p>
                            <p><strong>Issue:</strong> {escalation.IssueSummary}</p>
                        </div>
                        <p>Please review and respond as soon as possible.</p>
                    </body>
                    </html>
                ";

                await _emailService.SendEmailAsync(adminEmail, subject, body, true);
                _logger.LogInformation($"Assignment notification sent to admin {escalation.AssignedToAdminId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending admin assignment notification");
            }
        }

        public async Task<EscalationStatistics> GetEscalationStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                // Get all escalations and filter in memory
                // Note: For date range filtering with Firestore, we'd need more complex query methods
                var escalations = await _firestoreService.GetCollectionAsync<ChatbotEscalationDocument>(EscalationsCollection);

                // Apply date filters if provided
                if (startDate.HasValue)
                {
                    escalations = escalations.Where(e => e.CreatedAt.ToDateTime() >= startDate.Value.ToUniversalTime()).ToList();
                }
                if (endDate.HasValue)
                {
                    escalations = escalations.Where(e => e.CreatedAt.ToDateTime() <= endDate.Value.ToUniversalTime()).ToList();
                }

                var resolvedEscalations = escalations.Where(e => e.Status == "Resolved").ToList();

                var stats = new EscalationStatistics
                {
                    TotalEscalations = escalations.Count,
                    ResolvedEscalations = resolvedEscalations.Count,
                    PendingEscalations = escalations.Count(e => e.Status == "Pending" || e.Status == "Assigned" || e.Status == "InProgress"),
                    AverageResolutionTimeHours = resolvedEscalations.Any(e => e.ResolutionTimeHours.HasValue)
                        ? resolvedEscalations.Where(e => e.ResolutionTimeHours.HasValue).Average(e => e.ResolutionTimeHours!.Value)
                        : 0,
                    AverageResponseTimeMinutes = escalations.Any(e => e.ResponseTimeMinutes.HasValue)
                        ? escalations.Where(e => e.ResponseTimeMinutes.HasValue).Average(e => e.ResponseTimeMinutes!.Value)
                        : 0,
                    EscalationsByCategory = escalations
                        .GroupBy(e => e.Category ?? "Unknown")
                        .ToDictionary(g => g.Key, g => g.Count()),
                    EscalationsByPriority = escalations
                        .GroupBy(e => Enum.TryParse<EscalationPriority>(e.Priority, out var p) ? p : EscalationPriority.Normal)
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting escalation statistics");
                throw;
            }
        }

        private async Task NotifyUserOfResolutionAsync(ChatbotEscalationDto escalation)
        {
            try
            {
                var subject = $"Your Support Request Has Been Resolved";
                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>Support Request Resolved</h2>
                        <p>Hi {escalation.UserName},</p>
                        <p>Your support request has been resolved by our team.</p>
                        <div style='background: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p><strong>Resolution Notes:</strong></p>
                            <p>{escalation.ResolutionNotes}</p>
                        </div>
                        <p>If you need any further assistance, please don't hesitate to reach out.</p>
                        <p>Thank you for your patience!</p>
                        <p>Best regards,<br>Majoko Rentals Support Team</p>
                    </body>
                    </html>
                ";

                if (!string.IsNullOrEmpty(escalation.UserEmail))
                {
                    await _emailService.SendEmailAsync(escalation.UserEmail, subject, body, true);
                }

                _logger.LogInformation($"Resolution notification sent to user {escalation.UserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending resolution notification");
            }
        }

        private ChatbotEscalationDto MapToDto(ChatbotEscalationDocument escalation)
        {
            return new ChatbotEscalationDto
            {
                Id = int.TryParse(escalation.Id, out var id) ? id : 0,
                ChatConversationId = escalation.ChatConversationId,
                UserId = escalation.UserId,
                UserName = escalation.UserName,
                UserEmail = escalation.UserEmail,
                UserPhone = escalation.UserPhone,
                UserProfilePictureUrl = escalation.UserProfilePictureUrl,
                UserRole = Enum.TryParse<UserRole>(escalation.UserRole, out var role) ? role : UserRole.Tenant,
                Status = Enum.TryParse<EscalationStatus>(escalation.Status, out var status) ? status : EscalationStatus.Pending,
                Priority = Enum.TryParse<EscalationPriority>(escalation.Priority, out var priority) ? priority : EscalationPriority.Normal,
                EscalationReason = escalation.EscalationReason,
                Category = escalation.Category,
                AssignedToAdminId = escalation.AssignedToAdminId,
                AssignedToAdminName = escalation.AssignedToAdminName,
                CreatedAt = escalation.CreatedAt.ToDateTime(),
                AssignedAt = escalation.AssignedAt?.ToDateTime(),
                FirstResponseAt = escalation.FirstResponseAt?.ToDateTime(),
                ResolvedAt = escalation.ResolvedAt?.ToDateTime(),
                ResolutionNotes = escalation.ResolutionNotes,
                IssueSummary = escalation.IssueSummary,
                Tags = escalation.Tags,
                MessagingSessionId = int.TryParse(escalation.MessagingSessionId, out var msgId) ? msgId : null,
                EmailNotificationSent = escalation.EmailNotificationSent,
                InAppNotificationCreated = escalation.InAppNotificationCreated,
                ResponseTimeMinutes = escalation.ResponseTimeMinutes,
                ResolutionTimeHours = escalation.ResolutionTimeHours,
                RecentMessages = escalation.RecentMessages?.Select(m => new ChatMessageSummaryDto
                {
                    Id = m.Id,
                    Message = m.Message,
                    IsFromUser = m.IsFromUser,
                    SentAt = m.SentAt.ToDateTime(),
                    Intent = m.Intent
                }).ToList() ?? new List<ChatMessageSummaryDto>()
            };
        }
    }
}
