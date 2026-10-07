using Google.Cloud.Firestore;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Models.Firestore;
using Microsoft.Extensions.Logging;

namespace MajokoRentals.Core.Services
{
    /// <summary>
    /// Firestore-based messaging service for real-time messaging sessions
    /// </summary>
    public class MessagingService : IMessagingService
    {
        private readonly IFirestoreService _firestoreService;
        private readonly IEmailService _emailService;
        private readonly ILogger<MessagingService> _logger;

        private const string SessionsCollection = "messaging-sessions";
        private const string MessagesSubcollection = "messages";

        public MessagingService(
            IFirestoreService firestoreService,
            IEmailService emailService,
            ILogger<MessagingService> logger)
        {
            _firestoreService = firestoreService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<MessagingSessionDto> CreateSessionRequestAsync(string initiatorId, CreateMessagingSessionRequest request)
        {
            try
            {
                // Get initiator details from Firestore users collection
                var initiator = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>("users", initiatorId);

                string initiatorName = "Unknown User";
                if (initiator != null)
                {
                    var firstName = initiator.ContainsKey("firstName") ? initiator["firstName"]?.ToString() : "";
                    var lastName = initiator.ContainsKey("lastName") ? initiator["lastName"]?.ToString() : "";
                    initiatorName = $"{firstName} {lastName}".Trim();
                    if (string.IsNullOrWhiteSpace(initiatorName))
                    {
                        initiatorName = initiator.ContainsKey("email") ? initiator["email"]?.ToString() ?? "Unknown User" : "Unknown User";
                    }
                }

                var session = new MessagingSessionDocument
                {
                    InitiatorId = initiatorId,
                    InitiatorName = initiatorName,
                    InitiatorEmail = initiator?.ContainsKey("email") == true ? initiator["email"]?.ToString() : null,
                    InitiatorProfilePictureUrl = initiator?.ContainsKey("profileImageUrl") == true ? initiator["profileImageUrl"]?.ToString() : null,
                    ParticipantId = request.ParticipantId,
                    SessionType = request.SessionType.ToString(),
                    Subject = request.Subject,
                    Description = request.Description,
                    ApprovalStatus = "Pending",
                    RelatedPropertyId = request.RelatedPropertyId?.ToString(),
                    RelatedMaintenanceRequestId = request.RelatedMaintenanceRequestId,
                    RelatedLeaseId = request.RelatedLeaseId,
                    IsActive = false,
                    RequestedAt = Timestamp.GetCurrentTimestamp(),
                    Priority = request.Priority.ToString(),
                    UnreadCountInitiator = 0,
                    UnreadCountParticipant = 0,
                    CreatedAt = Timestamp.GetCurrentTimestamp(),
                    UpdatedAt = Timestamp.GetCurrentTimestamp()
                };

                // Get participant details if specified
                if (!string.IsNullOrEmpty(request.ParticipantId))
                {
                    var participant = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>("users", request.ParticipantId);
                    if (participant != null)
                    {
                        var firstName = participant.ContainsKey("firstName") ? participant["firstName"]?.ToString() : "";
                        var lastName = participant.ContainsKey("lastName") ? participant["lastName"]?.ToString() : "";
                        var participantName = $"{firstName} {lastName}".Trim();
                        session.ParticipantName = string.IsNullOrWhiteSpace(participantName) ?
                            (participant.ContainsKey("email") ? participant["email"]?.ToString() : "Unknown User") :
                            participantName;
                        session.ParticipantEmail = participant.ContainsKey("email") ? participant["email"]?.ToString() : null;
                        session.ParticipantProfilePictureUrl = participant.ContainsKey("profileImageUrl") ? participant["profileImageUrl"]?.ToString() : null;
                    }
                }

                // Save to Firestore
                var docRef = await _firestoreService.AddDocumentAsync(SessionsCollection, session);
                session.Id = docRef;

                _logger.LogInformation($"Messaging session created: {session.Id} by {initiatorId}");

                // Send notification
                await NotifySessionRequestAsync(MapToDto(session));

                return MapToDto(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating messaging session for user {initiatorId}");
                throw;
            }
        }

        public async Task<MessagingSessionDto> ApproveSessionAsync(string sessionId, string approvedById, ApproveSessionRequest request)
        {
            try
            {
                var session = await _firestoreService.GetDocumentAsync<MessagingSessionDocument>(SessionsCollection, sessionId);
                if (session == null)
                {
                    throw new Exception($"Session {sessionId} not found");
                }

                // Get approver details
                var approver = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>("users", approvedById);

                string approverName = "Unknown";
                if (approver != null)
                {
                    var firstName = approver.ContainsKey("firstName") ? approver["firstName"]?.ToString() : "";
                    var lastName = approver.ContainsKey("lastName") ? approver["lastName"]?.ToString() : "";
                    approverName = $"{firstName} {lastName}".Trim();
                    if (string.IsNullOrWhiteSpace(approverName))
                    {
                        approverName = approver.ContainsKey("email") ? approver["email"]?.ToString() ?? "Unknown" : "Unknown";
                    }
                }

                var updates = new Dictionary<string, object>
                {
                    { "ApprovalStatus", request.Approved ? "Approved" : "Rejected" },
                    { "ApprovedById", approvedById },
                    { "ApprovedByName", approverName },
                    { "ApprovedAt", Timestamp.GetCurrentTimestamp() },
                    { "IsActive", request.Approved },
                    { "UpdatedAt", Timestamp.GetCurrentTimestamp() }
                };

                if (request.Approved)
                {
                    updates["StartedAt"] = Timestamp.GetCurrentTimestamp();

                    // If this is an unassigned session (e.g., chatbot escalation), assign it to the approver
                    if (string.IsNullOrEmpty(session.ParticipantId))
                    {
                        updates["ParticipantId"] = approvedById;
                        updates["ParticipantName"] = approverName;
                        if (approver != null)
                        {
                            updates["ParticipantEmail"] = approver.ContainsKey("email") ? approver["email"]?.ToString() : null;
                            updates["ParticipantProfilePictureUrl"] = approver.ContainsKey("profileImageUrl") ? approver["profileImageUrl"]?.ToString() : null;
                        }
                        _logger.LogInformation($"Assigned unassigned session {sessionId} to approver {approvedById}");
                    }
                }
                else
                {
                    updates["RejectionReason"] = request.RejectionReason ?? "No reason provided";
                }

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    updates["Notes"] = request.Notes;
                }

                await _firestoreService.UpdateDocumentAsync(SessionsCollection, sessionId, updates);

                // Get updated session
                session = await _firestoreService.GetDocumentAsync<MessagingSessionDocument>(SessionsCollection, sessionId);

                _logger.LogInformation($"Session {sessionId} {(request.Approved ? "approved" : "rejected")} by {approvedById}");

                // Send notification to initiator
                await NotifySessionApprovedAsync(MapToDto(session!));

                return MapToDto(session!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving session {sessionId}");
                throw;
            }
        }

        public async Task<MessagingSessionDto> RejectSessionAsync(string sessionId, string rejectedById, string rejectionReason)
        {
            var request = new ApproveSessionRequest
            {
                Approved = false,
                RejectionReason = rejectionReason
            };
            return await ApproveSessionAsync(sessionId, rejectedById, request);
        }

        public async Task<MessagingSessionDto?> GetSessionByIdAsync(string sessionId)
        {
            try
            {
                var session = await _firestoreService.GetDocumentAsync<MessagingSessionDocument>(SessionsCollection, sessionId);
                return session != null ? MapToDto(session) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting session {sessionId}");
                throw;
            }
        }

        public async Task<List<MessagingSessionDto>> GetUserSessionsAsync(string userId, bool activeOnly = true)
        {
            try
            {
                var filters = new Dictionary<string, object>
                {
                    { "InitiatorId", userId }
                };

                if (activeOnly)
                {
                    filters["IsActive"] = true;
                }

                var sessions = await _firestoreService.QueryCollectionWithMultipleFiltersAsync<MessagingSessionDocument>(
                    SessionsCollection,
                    filters
                );

                // Also get sessions where user is participant
                var participantFilters = new Dictionary<string, object>
                {
                    { "ParticipantId", userId }
                };

                if (activeOnly)
                {
                    participantFilters["IsActive"] = true;
                }

                var participantSessions = await _firestoreService.QueryCollectionWithMultipleFiltersAsync<MessagingSessionDocument>(
                    SessionsCollection,
                    participantFilters
                );

                var allSessions = sessions.Select(MapToDto).ToList();
                allSessions.AddRange(participantSessions.Select(MapToDto));

                return allSessions.OrderByDescending(s => s.RequestedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting sessions for user {userId}");
                throw;
            }
        }

        public async Task<PendingSessionsSummary> GetPendingSessionsAsync(string userId)
        {
            try
            {
                // Get all pending sessions (both targeted and unassigned)
                var allPendingSessions = await _firestoreService.QueryCollectionAsync<MessagingSessionDocument>(
                    SessionsCollection,
                    "ApprovalStatus",
                    "Pending"
                );

                // Filter to sessions targeted to this user OR unassigned (ParticipantId is null)
                var relevantSessions = allPendingSessions
                    .Where(s => s.ParticipantId == userId || string.IsNullOrEmpty(s.ParticipantId))
                    .OrderByDescending(s => s.RequestedAt)
                    .Take(10)
                    .ToList();

                var sessionDtos = relevantSessions.Select(MapToDto).ToList();

                return new PendingSessionsSummary
                {
                    TotalPending = sessionDtos.Count,
                    HighPriority = sessionDtos.Count(s => s.Priority == SessionPriority.High),
                    UrgentPriority = sessionDtos.Count(s => s.Priority == SessionPriority.Urgent),
                    RecentRequests = sessionDtos
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting pending sessions for user {userId}");
                throw;
            }
        }

        public async Task EndSessionAsync(string sessionId)
        {
            try
            {
                var updates = new Dictionary<string, object>
                {
                    { "IsActive", false },
                    { "EndedAt", Timestamp.GetCurrentTimestamp() },
                    { "UpdatedAt", Timestamp.GetCurrentTimestamp() }
                };

                await _firestoreService.UpdateDocumentAsync(SessionsCollection, sessionId, updates);
                _logger.LogInformation($"Session {sessionId} ended");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error ending session {sessionId}");
                throw;
            }
        }

        public async Task<SessionMessageDto> SendMessageAsync(string senderId, SendMessageRequest request)
        {
            try
            {
                // Get sender details
                var sender = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>("users", senderId);

                string senderName = "Unknown User";
                if (sender != null)
                {
                    var firstName = sender.ContainsKey("firstName") ? sender["firstName"]?.ToString() : "";
                    var lastName = sender.ContainsKey("lastName") ? sender["lastName"]?.ToString() : "";
                    senderName = $"{firstName} {lastName}".Trim();
                    if (string.IsNullOrWhiteSpace(senderName))
                    {
                        senderName = sender.ContainsKey("email") ? sender["email"]?.ToString() ?? "Unknown User" : "Unknown User";
                    }
                }

                var message = new SessionMessageDocument
                {
                    SessionId = request.SessionId,
                    SenderId = senderId,
                    SenderName = senderName,
                    SenderProfilePictureUrl = sender?.ContainsKey("profileImageUrl") == true ? sender["profileImageUrl"]?.ToString() : null,
                    Content = request.Content,
                    MessageType = request.MessageType.ToString(),
                    SentAt = Timestamp.GetCurrentTimestamp(),
                    IsRead = false,
                    AttachmentUrls = request.AttachmentUrls,
                    ReplyToMessageId = request.ReplyToMessageId?.ToString(),
                    IsEdited = false,
                    IsDeleted = false
                };

                // Get reply-to message details if applicable
                if (request.ReplyToMessageId.HasValue)
                {
                    var replyToMessage = await GetMessageByIdAsync(request.SessionId.ToString(), request.ReplyToMessageId.Value.ToString());
                    if (replyToMessage != null)
                    {
                        message.ReplyToMessageContent = replyToMessage.Content;
                        message.ReplyToMessageSenderName = replyToMessage.SenderName;
                    }
                }

                // Save message to subcollection
                var docRef = await _firestoreService.AddSubcollectionDocumentAsync(
                    SessionsCollection,
                    request.SessionId.ToString(),
                    MessagesSubcollection,
                    message);
                message.Id = docRef;

                // Update session with last message info and increment unread count
                var session = await _firestoreService.GetDocumentAsync<MessagingSessionDocument>(SessionsCollection, request.SessionId.ToString());
                if (session != null)
                {
                    var sessionUpdates = new Dictionary<string, object>
                    {
                        { "LastMessageContent", message.Content },
                        { "LastMessageAt", message.SentAt },
                        { "LastMessageSenderId", senderId },
                        { "UpdatedAt", Timestamp.GetCurrentTimestamp() }
                    };

                    // Increment unread count for recipient
                    if (senderId == session.InitiatorId)
                    {
                        sessionUpdates["UnreadCountParticipant"] = session.UnreadCountParticipant + 1;
                    }
                    else
                    {
                        sessionUpdates["UnreadCountInitiator"] = session.UnreadCountInitiator + 1;
                    }

                    await _firestoreService.UpdateDocumentAsync(SessionsCollection, request.SessionId.ToString(), sessionUpdates);
                }

                _logger.LogInformation($"Message sent in session {request.SessionId} by {senderId}");

                // Send notification
                await NotifyNewMessageAsync(MapMessageToDto(message));

                return MapMessageToDto(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending message in session {request.SessionId}");
                throw;
            }
        }

        public async Task<SessionMessagesResponse> GetSessionMessagesAsync(string sessionId, int pageNumber = 1, int pageSize = 50)
        {
            try
            {
                var messages = await _firestoreService.QuerySubcollectionAsync<SessionMessageDocument>(
                    SessionsCollection,
                    sessionId,
                    MessagesSubcollection,
                    orderByField: "SentAt",
                    descending: true,
                    limit: pageSize
                );

                var messageDtos = messages.Select(MapMessageToDto).Reverse().ToList();

                // Get total count of messages in subcollection
                var allMessages = await _firestoreService.GetSubcollectionAsync<SessionMessageDocument>(
                    SessionsCollection,
                    sessionId,
                    MessagesSubcollection
                );
                var totalCount = allMessages.Count;

                return new SessionMessagesResponse
                {
                    SessionId = sessionId,
                    Messages = messageDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    HasMore = (pageNumber * pageSize) < totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting messages for session {sessionId}");
                throw;
            }
        }

        public async Task MarkMessagesAsReadAsync(List<string> messageIds, string userId)
        {
            try
            {
                // This is a simplified version - in production you'd batch these updates
                foreach (var messageId in messageIds)
                {
                    // You'd need to store session ID with message ID or query for it
                    _logger.LogInformation($"Marking message {messageId} as read by {userId}");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read");
                throw;
            }
        }

        public async Task<int> GetUnreadMessageCountAsync(string sessionId, string userId)
        {
            try
            {
                var session = await _firestoreService.GetDocumentAsync<MessagingSessionDocument>(SessionsCollection, sessionId);
                if (session == null) return 0;

                // Return appropriate unread count based on user role in session
                if (userId == session.InitiatorId)
                {
                    return session.UnreadCountInitiator;
                }
                else if (userId == session.ParticipantId)
                {
                    return session.UnreadCountParticipant;
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting unread count for session {sessionId}");
                throw;
            }
        }

        public async Task NotifySessionRequestAsync(MessagingSessionDto session)
        {
            try
            {
                if (!string.IsNullOrEmpty(session.ParticipantId))
                {
                    var subject = $"New Messaging Session Request: {session.Subject}";

                    var priorityBadge = session.Priority switch
                    {
                        SessionPriority.Urgent => "<span style='background-color: #dc3545; color: white; padding: 4px 8px; border-radius: 3px; font-size: 12px; font-weight: bold;'>URGENT</span>",
                        SessionPriority.High => "<span style='background-color: #ffc107; color: #151A2C; padding: 4px 8px; border-radius: 3px; font-size: 12px; font-weight: bold;'>HIGH PRIORITY</span>",
                        _ => "<span style='background-color: #6c757d; color: white; padding: 4px 8px; border-radius: 3px; font-size: 12px; font-weight: bold;'>NORMAL</span>"
                    };

                    var body = GetEmailTemplate("New Messaging Request", $@"
                        <h2>New Messaging Session Request</h2>
                        <p>You have received a new messaging request from a user.</p>

                        <div class='info-box'>
                            <h3>Request Details</h3>
                            <div class='info-item'><strong>From:</strong> {session.InitiatorName}</div>
                            <div class='info-item'><strong>Subject:</strong> {session.Subject}</div>
                            <div class='info-item'><strong>Priority:</strong> {priorityBadge}</div>
                            <div class='info-item'><strong>Date:</strong> {session.RequestedAt:dd MMMM yyyy HH:mm}</div>
                        </div>

                        {(!string.IsNullOrEmpty(session.Description) ? $@"
                        <div class='info-box'>
                            <h3>Message</h3>
                            <p style='margin: 0; color: #555555;'>{session.Description}</p>
                        </div>" : "")}

                        <div class='alert-box'>
                            <strong>Action Required:</strong><br>
                            Please log in to review this request and approve or reject it.
                        </div>

                        <p>Once approved, you'll be able to communicate directly with the user through the messaging system.</p>

                        <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>");

                    // Get participant email from Firestore
                    var participant = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>("users", session.ParticipantId);
                    var participantEmail = participant?["email"]?.ToString();

                    if (!string.IsNullOrEmpty(participantEmail))
                    {
                        await _emailService.SendEmailAsync(participantEmail, subject, body, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending session request notification");
            }
        }

        public async Task NotifySessionApprovedAsync(MessagingSessionDto session)
        {
            try
            {
                var subject = session.ApprovalStatus == ApprovalStatus.Approved
                    ? $"Messaging Session Approved: {session.Subject}"
                    : $"Messaging Session Rejected: {session.Subject}";

                var body = session.ApprovalStatus == ApprovalStatus.Approved
                    ? GetEmailTemplate("Messaging Session Approved", $@"
                        <h2>Messaging Session Approved!</h2>
                        <p>Good news! Your messaging request has been approved.</p>

                        <div class='alert-box success'>
                            <strong>✓ Request Approved!</strong><br>
                            Your messaging session has been approved by {session.ApprovedByName}. You can now start communicating.
                        </div>

                        <div class='info-box'>
                            <h3>Session Details</h3>
                            <div class='info-item'><strong>Subject:</strong> {session.Subject}</div>
                            <div class='info-item'><strong>Approved By:</strong> {session.ApprovedByName}</div>
                            <div class='info-item'><strong>Approved On:</strong> {session.ApprovedAt?.ToString("dd MMMM yyyy HH:mm")}</div>
                        </div>

                        <p style='text-align: center;'>
                            <a href='#' class='button'>View Chat</a>
                        </p>

                        <p>You can access this conversation through your account's messaging section or by clicking the button above.</p>

                        <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>")
                    : GetEmailTemplate("Messaging Session Rejected", $@"
                        <h2>Messaging Session Update</h2>
                        <p>Thank you for your messaging request. After review, we are unable to approve your request at this time.</p>

                        <div class='info-box'>
                            <h3>Request Details</h3>
                            <div class='info-item'><strong>Subject:</strong> {session.Subject}</div>
                            <div class='info-item'><strong>Reviewed By:</strong> {session.ApprovedByName}</div>
                            <div class='info-item'><strong>Date:</strong> {session.ApprovedAt?.ToString("dd MMMM yyyy HH:mm")}</div>
                        </div>

                        {(!string.IsNullOrEmpty(session.RejectionReason) ? $@"
                        <div class='alert-box'>
                            <strong>Reason for Rejection:</strong><br>
                            {session.RejectionReason}
                        </div>" : "")}

                        <p>If you have questions about this decision or would like to discuss alternative options, please feel free to contact us.</p>

                        <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>");

                // Get initiator email
                var initiator = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>("users", session.InitiatorId);
                var initiatorEmail = initiator?["email"]?.ToString();

                if (!string.IsNullOrEmpty(initiatorEmail))
                {
                    await _emailService.SendEmailAsync(initiatorEmail, subject, body, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending session approval notification");
            }
        }

        public async Task NotifyNewMessageAsync(SessionMessageDto message)
        {
            try
            {
                // In production, this would trigger a real-time notification via Firestore listeners
                // or push notifications to mobile devices
                _logger.LogInformation($"New message notification for session {message.SessionId} from {message.SenderName}");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending new message notification");
            }
        }

        // Helper methods
        private async Task<SessionMessageDocument?> GetMessageByIdAsync(string sessionId, string messageId)
        {
            var messagePath = $"{SessionsCollection}/{sessionId}/{MessagesSubcollection}";
            return await _firestoreService.GetDocumentAsync<SessionMessageDocument>(messagePath, messageId);
        }

        private MessagingSessionDto MapToDto(MessagingSessionDocument session)
        {
            return new MessagingSessionDto
            {
                Id = session.Id ?? string.Empty,
                InitiatorId = session.InitiatorId,
                InitiatorName = session.InitiatorName,
                InitiatorProfilePictureUrl = session.InitiatorProfilePictureUrl,
                ParticipantId = session.ParticipantId,
                ParticipantName = session.ParticipantName,
                ParticipantProfilePictureUrl = session.ParticipantProfilePictureUrl,
                SessionType = Enum.TryParse<SessionType>(session.SessionType, out var st) ? st : SessionType.UserToUser,
                Subject = session.Subject,
                Description = session.Description,
                ApprovalStatus = Enum.TryParse<ApprovalStatus>(session.ApprovalStatus, out var status) ? status : ApprovalStatus.Pending,
                ApprovedById = session.ApprovedById,
                ApprovedByName = session.ApprovedByName,
                ApprovedAt = session.ApprovedAt?.ToDateTime(),
                RejectionReason = session.RejectionReason,
                RelatedPropertyId = long.TryParse(session.RelatedPropertyId, out var propId) ? propId : null,
                RelatedPropertyName = session.RelatedPropertyName,
                RelatedMaintenanceRequestId = session.RelatedMaintenanceRequestId,
                RelatedLeaseId = session.RelatedLeaseId,
                RelatedChatbotEscalationId = int.TryParse(session.RelatedChatbotEscalationId, out var escId) ? escId : null,
                IsActive = session.IsActive,
                RequestedAt = session.RequestedAt.ToDateTime(),
                StartedAt = session.StartedAt?.ToDateTime(),
                EndedAt = session.EndedAt?.ToDateTime(),
                Priority = Enum.TryParse<SessionPriority>(session.Priority, out var priority) ? priority : SessionPriority.Normal,
                UnreadMessageCount = 0, // Will be calculated based on user
                LastMessage = !string.IsNullOrEmpty(session.LastMessageContent) ? new SessionMessageDto
                {
                    Content = session.LastMessageContent,
                    SentAt = session.LastMessageAt?.ToDateTime() ?? DateTime.UtcNow
                } : null,
                Notes = session.Notes
            };
        }

        private SessionMessageDto MapMessageToDto(SessionMessageDocument message)
        {
            return new SessionMessageDto
            {
                Id = message.Id ?? string.Empty,
                SessionId = message.SessionId ?? string.Empty,
                SenderId = message.SenderId,
                SenderName = message.SenderName,
                SenderProfilePictureUrl = message.SenderProfilePictureUrl,
                Content = message.Content,
                MessageType = Enum.TryParse<MessageType>(message.MessageType, out var type) ? type : MessageType.Text,
                SentAt = message.SentAt.ToDateTime(),
                IsRead = message.IsRead,
                ReadAt = message.ReadAt?.ToDateTime(),
                AttachmentUrls = message.AttachmentUrls,
                IsEdited = message.IsEdited,
                EditedAt = message.EditedAt?.ToDateTime(),
                IsDeleted = message.IsDeleted,
                ReplyToMessageId = int.TryParse(message.ReplyToMessageId, out var replyId) ? replyId : null
            };
        }

        private string GetEmailTemplate(string title, string content)
        {
            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{title}</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #1a1a1a;
            background-color: #f4f4f4;
            margin: 0;
            padding: 0;
        }}
        .email-container {{
            max-width: 600px;
            margin: 20px auto;
            background-color: #ffffff;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }}
        .email-header {{
            background: linear-gradient(135deg, #151A2C 0%, #2d3548 100%);
            color: #BAAA49;
            padding: 30px 20px;
            text-align: center;
        }}
        .email-header h1 {{
            margin: 0;
            font-size: 24px;
            font-weight: 600;
        }}
        .email-body {{
            padding: 30px 40px;
            background-color: #ffffff;
        }}
        .email-body h2 {{
            color: #151A2C;
            font-size: 20px;
            margin-top: 0;
        }}
        .email-body p {{
            margin: 15px 0;
            color: #333333;
        }}
        .info-box {{
            background-color: #f8f9fa;
            border-left: 4px solid #BAAA49;
            padding: 15px 20px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .info-box h3 {{
            margin: 0 0 10px 0;
            color: #151A2C;
            font-size: 16px;
        }}
        .info-item {{
            margin: 8px 0;
            color: #555555;
        }}
        .info-item strong {{
            color: #151A2C;
            display: inline-block;
            min-width: 140px;
        }}
        .button {{
            display: inline-block;
            padding: 12px 30px;
            margin: 20px 0;
            background-color: #BAAA49;
            color: #151A2C !important;
            text-decoration: none;
            border-radius: 5px;
            font-weight: 600;
            text-align: center;
        }}
        .button:hover {{
            background-color: #a89539;
        }}
        .alert-box {{
            background-color: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 15px 20px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .alert-box.danger {{
            background-color: #f8d7da;
            border-left-color: #dc3545;
        }}
        .alert-box.success {{
            background-color: #d1e7dd;
            border-left-color: #198754;
        }}
        .email-footer {{
            background-color: #f8f9fa;
            padding: 20px 40px;
            text-align: center;
            color: #6c757d;
            font-size: 14px;
            border-top: 1px solid #dee2e6;
        }}
        .email-footer p {{
            margin: 5px 0;
        }}
        .divider {{
            height: 1px;
            background-color: #dee2e6;
            margin: 25px 0;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='email-header'>
            <h1>Dr Majoko Projects</h1>
        </div>
        <div class='email-body'>
            {content}
        </div>
        <div class='email-footer'>
            <p><strong>Dr Majoko Projects (Pty) Ltd</strong></p>
            <p>Property Management Solutions</p>
            <p style='margin-top: 15px; font-size: 12px;'>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}
