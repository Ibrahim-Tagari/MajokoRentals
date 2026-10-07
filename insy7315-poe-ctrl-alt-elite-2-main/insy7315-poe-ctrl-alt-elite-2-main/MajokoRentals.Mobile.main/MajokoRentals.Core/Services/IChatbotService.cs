using MajokoRentals.Core.Models;

namespace MajokoRentals.Core.Services
{
    public interface IChatbotService
    {
        Task<string> GetResponseAsync(string userId, string message, Language language);
        Task<ChatConversation> StartConversationAsync(string userId);
        Task<bool> EscalateConversationAsync(int conversationId, string reason);
    }
}

