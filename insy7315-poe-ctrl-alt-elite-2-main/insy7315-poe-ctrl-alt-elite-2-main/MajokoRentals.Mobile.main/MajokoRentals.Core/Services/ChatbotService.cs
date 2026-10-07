using MajokoRentals.Core.Data;
using MajokoRentals.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace MajokoRentals.Core.Services
{
    public class ChatbotService : IChatbotService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatbotService> _logger;
        private readonly HttpClient _httpClient;

        public ChatbotService(ApplicationDbContext context, IConfiguration configuration, ILogger<ChatbotService> logger, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<string> GetResponseAsync(string userId, string message, Language language)
        {
            try
            {
                // Get or create conversation
                var conversation = await _context.ChatConversations
                    .Include(c => c.Messages)
                    .Where(c => c.UserId == userId && c.IsActive)
                    .OrderByDescending(c => c.StartedAt)
                    .FirstOrDefaultAsync();

                if (conversation == null)
                {
                    conversation = await StartConversationAsync(userId);
                }

                // Save user message
                var userMessage = new ChatMessage
                {
                    ConversationId = conversation.Id,
                    Message = message,
                    IsFromUser = true,
                    SentAt = DateTime.UtcNow
                };

                _context.ChatMessages.Add(userMessage);
                await _context.SaveChangesAsync();

                // Check for common queries first (rule-based)
                var response = await GetRuleBasedResponse(message, userId, language);

                if (response == null)
                {
                    // Use OpenAI for complex queries
                    response = await GetAIResponse(message, conversation.Messages.ToList(), language);
                }

                // Save bot response
                var botMessage = new ChatMessage
                {
                    ConversationId = conversation.Id,
                    Message = response,
                    IsFromUser = false,
                    SentAt = DateTime.UtcNow
                };

                _context.ChatMessages.Add(botMessage);

                // Check if escalation is needed
                if (conversation.Messages.Count(m => m.IsFromUser) >= 3 && response.Contains("I don't understand"))
                {
                    await EscalateConversationAsync(conversation.Id, "Unable to resolve query after 3 attempts");
                }

                await _context.SaveChangesAsync();

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Chatbot error: {ex.Message}");
                return TranslateMessage("I apologize, but I'm having trouble processing your request. Please try again or contact support.", language);
            }
        }

        private async Task<string?> GetRuleBasedResponse(string message, string userId, Language language)
        {
            message = message.ToLower();

            // Rent-related queries
            if (message.Contains("rent") || message.Contains("payment") || message.Contains("pay"))
            {
                var lease = await _context.Leases
                    .Include(l => l.Invoices)
                    .Where(l => l.TenantId == userId && l.Status == LeaseStatus.Active)
                    .FirstOrDefaultAsync();

                if (lease != null)
                {
                    var overdueInvoices = lease.Invoices.Where(i => i.Status == PaymentStatus.Overdue).ToList();
                    var pendingInvoices = lease.Invoices.Where(i => i.Status == PaymentStatus.Pending).ToList();

                    if (overdueInvoices.Any())
                    {
                        return TranslateMessage($"You have {overdueInvoices.Count} overdue invoice(s) totaling R{overdueInvoices.Sum(i => i.AmountDue):N2}. Please make payment as soon as possible.", language);
                    }
                    else if (pendingInvoices.Any())
                    {
                        var nextInvoice = pendingInvoices.OrderBy(i => i.DueDate).First();
                        return TranslateMessage($"Your next payment of R{nextInvoice.Amount:N2} is due on {nextInvoice.DueDate:dd MMMM yyyy}.", language);
                    }
                    else
                    {
                        return TranslateMessage("You're all caught up with your rent payments!", language);
                    }
                }
            }

            // Maintenance queries
            if (message.Contains("maintenance") || message.Contains("repair") || message.Contains("fix") || message.Contains("broken"))
            {
                var openRequests = await _context.MaintenanceRequests
                    .Where(m => m.ReportedById == userId && m.Status != MaintenanceStatus.Resolved)
                    .CountAsync();

                return TranslateMessage($"You have {openRequests} open maintenance request(s). To report a new issue, please visit the Maintenance section in your dashboard.", language);
            }

            // Lease queries
            if (message.Contains("lease") || message.Contains("contract") || message.Contains("tenancy"))
            {
                var lease = await _context.Leases
                    .Include(l => l.Property)
                    .Where(l => l.TenantId == userId && l.Status == LeaseStatus.Active)
                    .FirstOrDefaultAsync();

                if (lease != null)
                {
                    return TranslateMessage($"Your lease for {lease.Property.Name} ends on {lease.EndDate:dd MMMM yyyy}. Monthly rent is R{lease.MonthlyRent:N2}.", language);
                }
            }

            // Greeting
            if (message.Contains("hello") || message.Contains("hi") || message.Contains("hey") || message.Contains("greet"))
            {
                return TranslateMessage("Hello! I'm your rental assistant. How can I help you today? You can ask me about rent payments, maintenance requests, or your lease details.", language);
            }

            // Help query
            if (message.Contains("help"))
            {
                return TranslateMessage("I can help you with:\n- Rent payment information\n- Maintenance requests\n- Lease details\n- General property information\n\nWhat would you like to know?", language);
            }

            return null; // No rule-based response found
        }

        private async Task<string> GetAIResponse(string message, List<ChatMessage> conversationHistory, Language language)
        {
            try
            {
                var apiKey = _configuration["OpenAI:ApiKey"];
                if (string.IsNullOrEmpty(apiKey) || apiKey == "your-openai-api-key")
                {
                    return "I don't understand your question. Please try rephrasing or contact support for assistance.";
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = "You are a helpful assistant for Dr Majoko Projects rental management system. Answer questions about rent, leases, maintenance, and general property management. Keep responses concise and helpful. If you don't know something, suggest contacting support."
                    }
                };

                // Add conversation history (last 5 messages)
                foreach (var msg in conversationHistory.TakeLast(5))
                {
                    messages.Add(new
                    {
                        role = msg.IsFromUser ? "user" : "assistant",
                        content = msg.Message
                    });
                }

                messages.Add(new { role = "user", content = message });

                var requestBody = new
                {
                    model = _configuration["OpenAI:Model"] ?? "gpt-4",
                    messages = messages,
                    max_tokens = int.Parse(_configuration["OpenAI:MaxTokens"] ?? "500"),
                    temperature = 0.7
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                    var botResponse = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                    return TranslateMessage(botResponse ?? "I don't understand.", language);
                }

                return "I'm having trouble connecting to my AI service. Please try again later.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"OpenAI API error: {ex.Message}");
                return "I don't understand your question. Please try rephrasing or contact support for assistance.";
            }
        }

        public async Task<ChatConversation> StartConversationAsync(string userId)
        {
            var conversation = new ChatConversation
            {
                UserId = userId,
                StartedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.ChatConversations.Add(conversation);
            await _context.SaveChangesAsync();

            return conversation;
        }

        public async Task<bool> EscalateConversationAsync(int conversationId, string reason)
        {
            var conversation = await _context.ChatConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null) return false;

            conversation.IsEscalated = true;
            conversation.EscalatedAt = DateTime.UtcNow;
            conversation.EscalationReason = reason;
            conversation.IsActive = false;

            // Notify property manager (you can implement this)
            _logger.LogInformation($"Conversation {conversationId} escalated: {reason}");

            await _context.SaveChangesAsync();
            return true;
        }

        private string TranslateMessage(string message, Language language)
        {
            // This is a simplified translation. In production, use a proper translation service
            if (language == Language.Zulu)
            {
                // Basic Zulu translations
                var translations = new Dictionary<string, string>
                {
                    { "Hello", "Sawubona" },
                    { "Payment", "Ukukhokha" },
                    { "Rent", "Irenti" },
                    { "Thank you", "Ngiyabonga" }
                };

                foreach (var translation in translations)
                {
                    message = message.Replace(translation.Key, translation.Value);
                }
            }
            else if (language == Language.Sesotho)
            {
                // Basic Sesotho translations
                var translations = new Dictionary<string, string>
                {
                    { "Hello", "Dumela" },
                    { "Payment", "Tefo" },
                    { "Rent", "Rente" },
                    { "Thank you", "Kea leboha" }
                };

                foreach (var translation in translations)
                {
                    message = message.Replace(translation.Key, translation.Value);
                }
            }

            return message;
        }
    }
}

