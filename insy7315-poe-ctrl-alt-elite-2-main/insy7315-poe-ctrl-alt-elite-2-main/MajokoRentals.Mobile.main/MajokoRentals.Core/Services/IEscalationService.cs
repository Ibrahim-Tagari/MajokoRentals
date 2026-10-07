using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;

namespace MajokoRentals.Core.Services
{
    /// <summary>
    /// Service for managing chatbot escalations
    /// </summary>
    public interface IEscalationService
    {
        // Escalation Management
        Task<ChatbotEscalationDto> CreateEscalationAsync(string userId, CreateEscalationRequest request);
        Task<ChatbotEscalationDto> AssignEscalationAsync(string escalationId, string adminId);
        Task<ChatbotEscalationDto> UpdateEscalationStatusAsync(string escalationId, UpdateEscalationStatusRequest request);
        Task<ChatbotEscalationDto> ResolveEscalationAsync(string escalationId, string adminId, ResolveEscalationRequest request);

        // Retrieval
        Task<ChatbotEscalationDto?> GetEscalationByIdAsync(string escalationId);
        Task<EscalationsResponse> GetPendingEscalationsAsync(int pageNumber = 1, int pageSize = 20);
        Task<EscalationsResponse> GetAdminEscalationsAsync(string adminId, int pageNumber = 1, int pageSize = 20);
        Task<PendingEscalationsSummary> GetEscalationsSummaryAsync();
        Task<List<AdminWorkloadDto>> GetAdminWorkloadAsync();

        // Response Management
        Task RespondToEscalationAsync(string escalationId, string adminId, RespondToEscalationRequest request);

        // Notifications
        Task NotifyAdminsOfEscalationAsync(ChatbotEscalationDto escalation);
        Task NotifyUserOfResponseAsync(ChatbotEscalationDto escalation, string response);
        Task NotifyAdminOfAssignmentAsync(ChatbotEscalationDto escalation);

        // Statistics
        Task<EscalationStatistics> GetEscalationStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
    }
}
