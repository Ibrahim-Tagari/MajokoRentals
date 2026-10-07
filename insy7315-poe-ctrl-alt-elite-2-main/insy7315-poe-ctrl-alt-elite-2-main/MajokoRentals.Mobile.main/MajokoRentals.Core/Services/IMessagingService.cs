using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;

namespace MajokoRentals.Core.Services
{
    /// <summary>
    /// Service for managing messaging sessions and messages
    /// </summary>
    public interface IMessagingService
    {
        // Session Management
        Task<MessagingSessionDto> CreateSessionRequestAsync(string initiatorId, CreateMessagingSessionRequest request);
        Task<MessagingSessionDto> ApproveSessionAsync(string sessionId, string approvedById, ApproveSessionRequest request);
        Task<MessagingSessionDto> RejectSessionAsync(string sessionId, string rejectedById, string rejectionReason);
        Task<MessagingSessionDto?> GetSessionByIdAsync(string sessionId);
        Task<List<MessagingSessionDto>> GetUserSessionsAsync(string userId, bool activeOnly = true);
        Task<PendingSessionsSummary> GetPendingSessionsAsync(string userId);
        Task EndSessionAsync(string sessionId);

        // Message Management
        Task<SessionMessageDto> SendMessageAsync(string senderId, SendMessageRequest request);
        Task<SessionMessagesResponse> GetSessionMessagesAsync(string sessionId, int pageNumber = 1, int pageSize = 50);
        Task MarkMessagesAsReadAsync(List<string> messageIds, string userId);
        Task<int> GetUnreadMessageCountAsync(string sessionId, string userId);

        // Notifications
        Task NotifySessionRequestAsync(MessagingSessionDto session);
        Task NotifySessionApprovedAsync(MessagingSessionDto session);
        Task NotifyNewMessageAsync(SessionMessageDto message);
    }
}
