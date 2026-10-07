using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using MajokoRentals.Core.Services;
using System.Security.Claims;

namespace MajokoRentals.API.Controllers
{
    // Rule-based chatbot assistant with English + isiZulu support
    // Role-aware routing - only suggests pages user has access to
    [Route("Chatbot")]
    [ApiController]
    public class ChatbotApiController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, int> UnknownStreak = new();
        private static readonly ConcurrentDictionary<string, ChatSession> Sessions = new();

        private readonly IEmailService _emailService;
        private readonly IUserProfileService _userProfile;
        private readonly ILogger<ChatbotApiController> _logger;
        private readonly IConfiguration _configuration;

        public ChatbotApiController(
            IEmailService emailService,
            IUserProfileService userProfile,
            ILogger<ChatbotApiController> logger,
            IConfiguration configuration)
        {
            _emailService = emailService;
            _userProfile = userProfile;
            _logger = logger;
            _configuration = configuration;
        }

        public sealed record ChatRequest(string? Message, string? Lang);
        public sealed record ChatResponse(string Reply, bool Escalated = false, int? TicketId = null, string? NavigateTo = null);
        public sealed record ChatSession(string UserId, string UserName, string UserEmail, string UserRole, List<string> Messages);

        private sealed record Rule(
            string Name,
            string PatternEn,      // English prompt patterns
            string PatternZu,      // isiZulu prompt patterns
            Func<string, string, string> ReplyFn,  // Function to generate reply based on role and lang
            bool Escalate = false, // escalate to human
            Func<string, string?>? NavigateToFn = null,  // Function to determine navigation based on role
            string[]? RequiredRoles = null  // Required roles to access this rule
        );

        private string GetUserRole()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return "Guest";

            if (User.IsInRole("Admin"))
                return "Admin";
            if (User.IsInRole("PropertyManager"))
                return "PropertyManager";
            if (User.IsInRole("Tenant"))
                return "Tenant";
            if (User.IsInRole("Caretaker"))
                return "Caretaker";

            return "Applicant"; // Default for authenticated users without specific role
        }

        // Role-based navigation helper - returns mobile app routes
        private string? GetNavigationUrl(string intent, string userRole)
        {
            return intent switch
            {
                "properties" => "/properties",
                "apply" => "/properties",
                "dashboard" when userRole != "Guest" => "/dashboard",
                "invoices" when userRole == "Tenant" => "/invoices/my",
                "invoices" when userRole is "Admin" or "PropertyManager" => "/invoices",
                "payments" when userRole == "Tenant" => "/invoices/my",
                "lease" when userRole == "Tenant" => "/leases/my",
                "leases" when userRole is "Admin" or "PropertyManager" => "/leases",
                "maintenance" when userRole == "Tenant" => "/maintenance/my",
                "maintenance" when userRole is "Caretaker" or "Admin" or "PropertyManager" => "/maintenance",
                "applications" when userRole is "Admin" or "PropertyManager" => "/applications",
                "myapplications" when userRole is "Applicant" or "Guest" => "/applications/my",
                _ => null
            };
        }

        // Generate role-specific reply
        private string GetRoleAwareReply(string baseReplyEn, string baseReplyZu, string userRole, string lang, string? navUrl = null)
        {
            var reply = lang == "zu" ? baseReplyZu : baseReplyEn;

            // Add role-specific context
            if (userRole == "Guest" && navUrl != null && navUrl.Contains("/my"))
            {
                reply += lang == "zu"
                    ? "\n\nSicela ungene nge-account yakho ukuze ufinyelele kulokhu."
                    : "\n\nPlease log in to your account to access this feature.";
                return reply;
            }

            return reply;
        }

        // First match wins. Patterns are case-insensitive; either language can trigger.
        private List<Rule> GetRules(string userRole)
        {
            return new List<Rule>
            {
                // Agent/Human escalation
                new("agent",
                    @"\b(agent|human|support|escalate|help me|speak to someone)\b",
                    @"\b(umsebenzi|umxhumanisi|osebenzayo|dlulisela|agent|ngisiza|khuluma nomuntu)\b",
                    (role, lang) => lang == "zu"
                        ? "Ngikuxhuma nomsebenzi womuntu. Bazokuxhumana maduze."
                        : "I'm connecting you with a human support agent. They will contact you shortly.",
                    Escalate: true),

                // Greeting
                new("greeting",
                    @"\b(hi|hello|hey|good (morning|afternoon|evening)|howzit|greetings)\b",
                    @"\b(sawubona|sanibonani|unjani|morning|kusile|ntambama|busuku|kunjani)\b",
                    (role, lang) =>
                    {
                        var greeting = lang == "zu"
                            ? $"Sawubona! Wamukelekile e-Dr Majoko Projects. 👋"
                            : $"Hi! Welcome to Dr Majoko Projects. 👋";

                        var help = role switch
                        {
                            "Admin" or "PropertyManager" => lang == "zu"
                                ? "Ngingakusiza ngokuphatha izakhiwo, izicelo, ama-lease, izinkokhelo, nokulungisa."
                                : "I can help you manage properties, applications, leases, payments, and maintenance.",
                            "Tenant" => lang == "zu"
                                ? "Ngingakusiza nge-lease yakho, izinkokhelo, ama-invoice, nokulungisa."
                                : "I can help you with your lease, payments, invoices, and maintenance requests.",
                            "Caretaker" => lang == "zu"
                                ? "Ngingakusiza ngezicelo zokulungisa kanye nezicelo zesevisi."
                                : "I can help you with maintenance requests and service tickets.",
                            "Applicant" => lang == "zu"
                                ? "Ngingakusiza ngezakhiwo ezitholakalayo kanye nezicelo zakho."
                                : "I can help you with available properties and your applications.",
                            _ => lang == "zu"
                                ? "Ngingakusiza ngezakhiwo ezitholakalayo, ukufaka izicelo, kanye nominye imininingwane."
                                : "I can help you with available properties, submitting applications, and general information."
                        };

                        return $"{greeting} {help} {(lang == "zu" ? "Ufuna ukwazi ngani?" : "What would you like to know?")}";
                    }),

                // General Help
                new("help",
                    @"\b(help|what can you do|how can you help|menu|options|commands)\b",
                    @"\b(usizo|ngenzenjani|ungasiza ngani|ungayenza ini|imenu|okukhethwa kukho)\b",
                    (role, lang) =>
                    {
                        var intro = lang == "zu" ? "Ngingakusiza nge:" : "I can help with:";
                        var options = role switch
                        {
                            "Admin" or "PropertyManager" => lang == "zu"
                                ? "• Ukuphatha izakhiwo\n• Ukubheka izicelo\n• Ukuphatha ama-lease\n• Izinkokhelo nama-invoice\n• Izicelo zokulungisa\n• Imibiko ne-analytics"
                                : "• Managing properties\n• Reviewing applications\n• Managing leases\n• Payments & invoices\n• Maintenance requests\n• Reports & analytics",
                            "Tenant" => lang == "zu"
                                ? "• Ukubheka i-lease yakho\n• Ukubheka ama-invoice nokukhokha\n• Ukubika ukulungisa\n• Ukubheka isimo sokulungisa\n• Imininingwane yokuxhumana"
                                : "• View your lease details\n• Check invoices & make payments\n• Report maintenance issues\n• Track maintenance status\n• Contact information",
                            "Caretaker" => lang == "zu"
                                ? "• Ukubheka izicelo zokulungisa\n• Ukuvuselela isimo sezicelo\n• Ukubika izinkinga\n• Amahora ehhovisi"
                                : "• View maintenance requests\n• Update request status\n• Report issues\n• Office hours",
                            _ => lang == "zu"
                                ? "• Ukubuka izakhiwo ezitholakalayo\n• Ukufaka izicelo zokugasha\n• Ukulandela izicelo zakho\n• Imininingwane yokuxhumana"
                                : "• View available properties\n• Submit rental applications\n• Track your applications\n• Contact information"
                        };

                        var footer = lang == "zu"
                            ? "\n\nYisho 'agent' noma nini ukukhuluma nomuntu."
                            : "\n\nSay 'agent' anytime to speak with a human.";

                        return $"{intro}\n{options}{footer}";
                    }),

                // Properties/Listings
                new("view_properties",
                    @"\b(properties|listings|available|browse|view properties|see properties|show me properties)\b",
                    @"\b(izakhiwo|uhlu|ekhona|thola izindlu|impahla|buka izakhiwo|bonisa izakhiwo)\b",
                    (role, lang) => lang == "zu"
                        ? "Ake ngikuyise ekhasini lezakhiwo ezitholakalayo lapho ungabheka zonke futhi ufake isicelo sokugasha."
                        : "Let me take you to our available properties page where you can browse all listings and apply for rentals.",
                    NavigateToFn: role => GetNavigationUrl("properties", role)),

                // Apply for rental
                new("apply",
                    @"\b(apply|application|submit application|how to apply|apply for rental)\b",
                    @"\b(faka isicelo|isicelo|thumela isicelo|apply|ngifake kanjani isicelo)\b",
                    (role, lang) =>
                    {
                        var reply = lang == "zu"
                            ? "Ukufake isicelo sokugasha:\n1. Yiya ku-Properties uthole iyunithi oyithandayo\n2. Chofooza ipropathi ukubona imininingwane\n3. Chofooza 'Apply Now'\n4. Gcwalisa imininingwane ufake i-ID + payslip"
                            : "To apply for a rental:\n1. Go to Properties and find a unit you like\n2. Click on the property to view details\n3. Click 'Apply Now'\n4. Fill in your details and upload ID + payslip";

                        if (role == "Guest")
                        {
                            reply += lang == "zu"
                                ? "\n\nSicela udale i-account noma ungene ukufaka isicelo."
                                : "\n\nPlease create an account or log in to submit an application.";
                        }
                        else
                        {
                            reply += lang == "zu"
                                ? "\n\nUngathanda ngikuyise ekhasini lezakhiwo?"
                                : "\n\nWould you like me to take you to the properties page?";
                        }

                        return reply;
                    },
                    NavigateToFn: role => GetNavigationUrl("properties", role)),

                // My Applications (for applicants)
                new("my_applications",
                    @"\b(my applications|application status|track application|check application)\b",
                    @"\b(izicelo zami|isimo sesicelo|landela isicelo|bheka isicelo)\b",
                    (role, lang) => role is "Admin" or "PropertyManager"
                        ? (lang == "zu"
                            ? "Ake ngikuyise ekhasini lokuphatha izicelo lapho ungabheka zonke izicelo ezingenayo."
                            : "Let me take you to the applications management page where you can review all incoming applications.")
                        : (lang == "zu"
                            ? "Ake ngikuyise ekhasini lakho lezicelo lapho ungabheka isimo sezicelo zakho zokugasha."
                            : "Let me take you to your applications page where you can check the status of your rental applications."),
                    NavigateToFn: role => role is "Admin" or "PropertyManager"
                        ? GetNavigationUrl("applications", role)
                        : GetNavigationUrl("myapplications", role)),

                // Invoices/Payments
                new("invoices",
                    @"\b(invoice|invoices|bill|billing|my invoices|view invoices|check invoice)\b",
                    @"\b(i-invoice|ama-invoice|ibhili|ukukhokha|ama-invoice ami|bheka i-invoice)\b",
                    (role, lang) => role == "Tenant"
                        ? (lang == "zu"
                            ? "Ake ngikuyise ekhasini lakho lama-invoice lapho ungabheka wonke ama-invoice nomlando wezinkokhelo."
                            : "Let me take you to your invoices page where you can view all your invoices and payment history.")
                        : role is "Admin" or "PropertyManager"
                        ? (lang == "zu"
                            ? "Ake ngikuyise ekhasini lezinkokhelo lapho ungabheka wonke ama-invoice ezakhiwo."
                            : "Let me take you to the payments page where you can view all property invoices.")
                        : (lang == "zu"
                            ? "Ama-invoice atholakala kuphela kubaqashi ababhalisile. Sicela ungene nge-account yakho yokuqasha."
                            : "Invoices are only available for registered tenants. Please log in to your tenant account."),
                    NavigateToFn: role => GetNavigationUrl("invoices", role)),

                // Make Payment
                new("make_payment",
                    @"\b(pay|payment|make payment|pay rent|pay invoice|how to pay)\b",
                    @"\b(khokha|inkokhelo|yenza inkokhelo|khokha irenti|khokha i-invoice|ngikhokha kanjani)\b",
                    (role, lang) =>
                    {
                        if (role != "Tenant")
                        {
                            return lang == "zu"
                                ? "Izinkokhelo zerenti zenzelwa abaqashi ababhalisile kuphela. Uma uwumqashi, sicela ungene nge-account yakho."
                                : "Rent payments are made by registered tenants only. If you're a tenant, please log in to your account.";
                        }

                        return lang == "zu"
                            ? "Ukwenza inkokhelo:\n1. Yiya ku-'My Invoices'\n2. Thola i-invoice ofuna ukuyikhokha\n3. Chofooza 'Pay Now'\n4. Faka imininingwane yenkokhelo\n\nUngathanda ngikuyise kuma-invoice akho?"
                            : "To make a payment:\n1. Go to 'My Invoices'\n2. Find the invoice you want to pay\n3. Click 'Pay Now'\n4. Enter your payment details\n\nWould you like me to take you to your invoices?";
                    },
                    NavigateToFn: role => role == "Tenant" ? GetNavigationUrl("payments", role) : null),

                // Lease Information
                new("lease",
                    @"\b(lease|contract|agreement|my lease|lease status|lease details)\b",
                    @"\b(i-lease|inkontileka|isivumelwano|i-lease yami|isimo se-lease)\b",
                    (role, lang) =>
                    {
                        if (role == "Tenant")
                        {
                            return lang == "zu"
                                ? "Ake ngikuyise emininingwaneni ye-lease yakho lapho ungabona isivumelwano sakho, izinsuku zokuqala/nokuphela, nesimo sokuvuselela."
                                : "Let me take you to your lease details where you can view your lease agreement, start/end dates, and renewal status.";
                        }
                        else if (role is "Admin" or "PropertyManager")
                        {
                            return lang == "zu"
                                ? "Ake ngikuyise ekhasini lokuphatha ama-lease lapho ungabheka wonke ama-lease ezakhiwo."
                                : "Let me take you to the lease management page where you can view all property leases.";
                        }
                        else
                        {
                            return lang == "zu"
                                ? "Imininingwane ye-lease itholakala kuphela kubaqashi ababhalisile. Uma usuqashile, sicela ungene nge-account yakho."
                                : "Lease information is only available for registered tenants. If you're a tenant, please log in to your account.";
                        }
                    },
                    NavigateToFn: role => GetNavigationUrl("lease", role) ?? GetNavigationUrl("leases", role)),

                // Maintenance
                new("maintenance",
                    @"\b(maintenance|repair|fault|problem|broken|not working|log maintenance|report issue)\b",
                    @"\b(ukulungisa|isilungiso|iphutha|inkinga|ephukile|akusebenzi|bika inkinga)\b",
                    (role, lang) =>
                    {
                        if (role is "Tenant" or "Caretaker")
                        {
                            return lang == "zu"
                                ? "Ukubika inkinga yokulungisa:\n1. Yiya ku-'Maintenance'\n2. Chofooza 'New Request'\n3. Khetha uhlobo nokubaluleka\n4. Engeza incazelo nezithombe\n5. Thumela\n\nAke ngikuyise ekhasini lokulungisa."
                                : "To report a maintenance issue:\n1. Go to 'Maintenance'\n2. Click 'New Request'\n3. Select category and urgency\n4. Add description and photos\n5. Submit\n\nLet me take you to the maintenance page.";
                        }
                        else if (role is "Admin" or "PropertyManager")
                        {
                            return lang == "zu"
                                ? "Ake ngikuyise ekhasini lokuphatha ukulungisa lapho ungabheka wonke amarequest okulungisa."
                                : "Let me take you to the maintenance management page where you can view all maintenance requests.";
                        }
                        else
                        {
                            return lang == "zu"
                                ? "Ukubika ukulungisa kutholakala kuphela kubaqashi noma abanakekeli ababhalisile."
                                : "Reporting maintenance is only available for registered tenants or caretakers.";
                        }
                    },
                    NavigateToFn: role => GetNavigationUrl("maintenance", role)),

                // Dashboard
                new("dashboard",
                    @"\b(dashboard|home|main page|overview|my dashboard)\b",
                    @"\b(dashboard|ikhaya|ikhasi elikhulu|ukubuka konke)\b",
                    (role, lang) =>
                    {
                        if (role == "Guest")
                        {
                            return lang == "zu"
                                ? "I-dashboard itholakala uma usulogile. Sicela ungene nge-account yakho."
                                : "The dashboard is available once you're logged in. Please log in to your account.";
                        }

                        var desc = role switch
                        {
                            "Admin" or "PropertyManager" => lang == "zu"
                                ? "I-dashboard yakho ibonisa:\n• Isifinyezo sezakhiwo\n• Izicelo ezintsha\n• Ama-lease asebenzayo\n• Izinkokhelo ezisalindile"
                                : "Your dashboard shows:\n• Properties overview\n• New applications\n• Active leases\n• Outstanding payments",
                            "Tenant" => lang == "zu"
                                ? "I-dashboard yakho ibonisa:\n• Isikweletu esiphelelayo\n• Ama-invoice akamuva\n• Isimo se-lease\n• Izicelo zokulungisa"
                                : "Your dashboard shows:\n• Outstanding balance\n• Recent invoices\n• Lease status\n• Maintenance requests",
                            _ => lang == "zu"
                                ? "I-dashboard yakho ibonisa:\n• Isimo sezicelo zakho\n• Izaziso zakamuva"
                                : "Your dashboard shows:\n• Your application status\n• Recent announcements"
                        };

                        return $"{desc}\n\n{(lang == "zu" ? "Ake ngikuyise ku-dashboard yakho." : "Let me take you to your dashboard.")}";
                    },
                    NavigateToFn: role => GetNavigationUrl("dashboard", role)),

                // Office Hours & Contact
                new("office_hours",
                    @"\b(office|hours|when open|opening times|closing time|working hours)\b",
                    @"\b(amahora|amahora ehhovisi|ivulekile|iyavalwa|amahora okusebenza)\b",
                    (role, lang) => lang == "zu"
                        ? "📍 Amahora Ehhovisi:\nUMsombuluko - ULwesihlanu: 08:00 - 17:00\nUMgqibelo - ISonto: Kuvalekile\n\n🚨 Ukulungisa okuphuthumayo kutholakala 24/7 ngomugqa wethu we-emergency."
                        : "📍 Office Hours:\nMonday - Friday: 08:00 - 17:00\nSaturday - Sunday: Closed\n\n🚨 Emergency maintenance is available 24/7 via our emergency line."),

                new("contact",
                    @"\b(contact|email|phone|number|reach|get in touch|call)\b",
                    @"\b(xhumana|imeyili|ifoni|inombolo|finyelela|shayela)\b",
                    (role, lang) => lang == "zu"
                        ? "📞 Imininingwane Yokuxhumana:\n\n📧 Ukwesekwa: support@majoko.local\n💰 Izinkokhelo: billing@majoko.local\n🚨 Ezimo Eziphuthumayo: Umugqa we-emergency (24/7)\n\n📍 Sivakashele: UMso-Lwe 08:00-17:00"
                        : "📞 Contact Information:\n\n📧 Support: support@majoko.local\n💰 Payments: billing@majoko.local\n🚨 Emergencies: Emergency line (24/7)\n\n📍 Visit us: Mon-Fri 08:00-17:00"),

                // Login/Account
                new("login",
                    @"\b(login|log in|sign ?in|account|password|forgot password)\b",
                    @"\b(ngena|ukungena|i-account|iphasiwedi|ngikhohlwe iphasiwedi)\b",
                    (role, lang) =>
                    {
                        if (role != "Guest")
                        {
                            return lang == "zu"
                                ? "Usungenile nge-account yakho. Ngingakusiza ngani namuhla?"
                                : "You're already logged in to your account. How can I help you today?";
                        }

                        return lang == "zu"
                            ? "Ukufinyelela ku-account yakho:\n1. Chofooza 'Login' kumenu ephezulu\n2. Faka i-email nephasiwedi yakho\n\nUkhohlwe iphasiwedi? Chofooza 'Forgot Password' ekhasini lokungena."
                            : "To access your account:\n1. Click 'Login' in the top menu\n2. Enter your email and password\n\nForgot password? Click 'Forgot Password' on the login page.";
                    }),

                // Polite responses
                new("thanks",
                    @"\b(thanks|thank you|appreciate|cheers|thx)\b",
                    @"\b(ngiyabonga|sibonga|ngiyabulela|siyabonga)\b",
                    (role, lang) => lang == "zu"
                        ? "Wamukelekile kakhulu! 😊 Kukhona okunye engingakusiza ngakho?"
                        : "You're very welcome! 😊 Is there anything else I can help you with?"),

                new("goodbye",
                    @"\b(bye|goodbye|see you|later|exit|close)\b",
                    @"\b(hamba kahle|sala kahle|ngizokubona|exit|vala)\b",
                    (role, lang) => lang == "zu"
                        ? "Hamba kahle! Khululeka ukukhuluma nami noma inini udinga usizo. Ube nosuku oluhle! 👋"
                        : "Goodbye! Feel free to chat with me anytime you need help. Have a great day! 👋")
            };
        }

        private static string NormalizeLang(string? lang)
        {
            lang = (lang ?? "").Trim().ToLowerInvariant();
            return (lang == "zu" || lang == "zulu" || lang == "isizulu") ? "zu" : "en";
        }

        private static bool MatchAny(Rule r, string text)
        {
            return Regex.IsMatch(text, r.PatternEn, RegexOptions.IgnoreCase | RegexOptions.Singleline) ||
                   Regex.IsMatch(text, r.PatternZu, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        [HttpGet("Commands")]
        [Produces("application/json")]
        public IActionResult Commands([FromQuery] string? lang)
        {
            var l = NormalizeLang(lang);
            var userRole = GetUserRole();
            return Ok(GetHelpGroups(l, userRole));
        }

        [HttpPost("SendMessage")]
        [Consumes("application/x-www-form-urlencoded")]
        [Produces("application/json")]
        public async Task<IActionResult> SendMessage([FromForm] string? message, [FromForm] string? lang)
        {
            var text = (message ?? string.Empty).Trim();
            var l = NormalizeLang(lang);
            var clientKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";
            var userRole = GetUserRole();

            // Track conversation
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var userProfile = await _userProfile.GetUserProfileAsync(userId);
                if (userProfile != null)
                {
                    if (!Sessions.ContainsKey(clientKey))
                    {
                        Sessions[clientKey] = new ChatSession(
                            userId,
                            $"{userProfile.FirstName} {userProfile.LastName}",
                            userProfile.Email ?? "unknown",
                            userRole,
                            new List<string>()
                        );
                    }
                    Sessions[clientKey].Messages.Add($"User ({userRole}): {text}");
                }
            }

            if (string.IsNullOrWhiteSpace(text))
                return Ok(new ChatResponse(l == "zu" ? "Sicela ufake umlayezo." : "Please enter a message."));

            var rules = GetRules(userRole);

            foreach (var r in rules)
            {
                if (MatchAny(r, text))
                {
                    UnknownStreak[clientKey] = 0;
                    var reply = r.ReplyFn(userRole, l);
                    var navUrl = r.NavigateToFn?.Invoke(userRole);

                    if (Sessions.ContainsKey(clientKey))
                    {
                        Sessions[clientKey].Messages.Add($"Bot: {reply}");
                    }

                    if (r.Escalate)
                    {
                        var id = await EscalateToHuman(clientKey, text, l);
                        return Ok(new ChatResponse(reply, Escalated: true, TicketId: id));
                    }

                    return Ok(new ChatResponse(reply, NavigateTo: navUrl));
                }
            }

            var hits = UnknownStreak.AddOrUpdate(clientKey, 1, (_, v) => v + 1);
            if (hits >= 3)
            {
                UnknownStreak[clientKey] = 0;
                var id = await EscalateToHuman(clientKey, text, l);
                var escalationReply = l == "zu"
                    ? "Angikwazi ukukusiza ngalokho. Ngikudlulisela kumuntu ozokusiza."
                    : "I'm not able to help with that. Let me connect you with a human support agent.";

                if (Sessions.ContainsKey(clientKey))
                {
                    Sessions[clientKey].Messages.Add($"Bot: {escalationReply}");
                }

                return Ok(new ChatResponse(escalationReply, Escalated: true, TicketId: id));
            }

            var suggestionReply = l == "zu"
                ? "Angiqiniseki. Zama ukuhlelwa kabusha noma uthi 'agent' ukuze kusizwe umuntu. Ungasho 'usizo' ukubona izinto engingakusiza ngazo."
                : "I'm not sure I understand. Try rephrasing your question, or say 'agent' to speak with a human. Say 'help' to see what I can assist with.";

            if (Sessions.ContainsKey(clientKey))
            {
                Sessions[clientKey].Messages.Add($"Bot: {suggestionReply}");
            }

            return Ok(new ChatResponse(suggestionReply));
        }

        private sealed record HelpGroup(string Title, string[] Examples);

        private List<HelpGroup> GetHelpGroups(string lang, string userRole)
        {
            if (lang == "zu")
            {
                return userRole switch
                {
                    "Admin" or "PropertyManager" => new List<HelpGroup>
                    {
                        new("Okuyisisekelo", new[] { "sawubona", "usizo", "xhumana nathi", "amahora ehhovisi" }),
                        new("Ukuphatha", new[] { "izakhiwo", "izicelo", "ama-lease", "izinkokhelo", "ukulungisa" }),
                        new("I-Account", new[] { "dashboard", "imibiko" }),
                        new("Ukwesekwa", new[] { "agent", "khuluma nomuntu" })
                    },
                    "Tenant" => new List<HelpGroup>
                    {
                        new("Okuyisisekelo", new[] { "sawubona", "usizo", "xhumana nathi", "amahora ehhovisi" }),
                        new("Izinkokhelo", new[] { "ama-invoice ami", "yenza inkokhelo", "faka ubufakazi benkokhelo" }),
                        new("I-Lease", new[] { "i-lease yami", "ukuvuselela i-lease" }),
                        new("Ukulungisa", new[] { "bika ukulungisa", "landela ukulungisa" }),
                        new("I-Account", new[] { "dashboard" }),
                        new("Ukwesekwa", new[] { "agent" })
                    },
                    _ => new List<HelpGroup>
                    {
                        new("Okuyisisekelo", new[] { "sawubona", "usizo", "xhumana nathi" }),
                        new("Izakhiwo", new[] { "buka izakhiwo", "faka isicelo" }),
                        new("Izicelo Zami", new[] { "izicelo zami", "landela isicelo" }),
                        new("I-Account", new[] { "ngena" })
                    }
                };
            }
            else
            {
                return userRole switch
                {
                    "Admin" or "PropertyManager" => new List<HelpGroup>
                    {
                        new("General", new[] { "hello", "help", "contact us", "office hours" }),
                        new("Management", new[] { "properties", "applications", "leases", "payments", "maintenance" }),
                        new("Account", new[] { "dashboard", "reports" }),
                        new("Support", new[] { "agent", "speak to human" })
                    },
                    "Tenant" => new List<HelpGroup>
                    {
                        new("General", new[] { "hello", "help", "contact us", "office hours" }),
                        new("Payments", new[] { "my invoices", "make payment", "upload proof" }),
                        new("Lease", new[] { "my lease", "lease renewal" }),
                        new("Maintenance", new[] { "report maintenance", "track maintenance" }),
                        new("Account", new[] { "dashboard" }),
                        new("Support", new[] { "agent" })
                    },
                    _ => new List<HelpGroup>
                    {
                        new("General", new[] { "hello", "help", "contact us" }),
                        new("Properties", new[] { "view properties", "submit application" }),
                        new("My Applications", new[] { "my applications", "track application" }),
                        new("Account", new[] { "login" })
                    }
                };
            }
        }

        private async Task<int> EscalateToHuman(string clientKey, string userMessage, string lang)
        {
            var ticketId = new Random().Next(1000, 9999);

            try
            {
                var session = Sessions.ContainsKey(clientKey) ? Sessions[clientKey] : null;
                var userName = session?.UserName ?? "Unknown User";
                var userEmail = session?.UserEmail ?? "Not logged in";
                var userId = session?.UserId ?? "Anonymous";
                var userRole = session?.UserRole ?? "Guest";

                var conversationHistory = session != null
                    ? string.Join("\n", session.Messages.TakeLast(10))
                    : $"User: {userMessage}";

                var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #151A2C; color: #BAAA49; padding: 20px; text-align: center; }}
        .content {{ background: #f9f9f9; padding: 20px; margin: 20px 0; }}
        .info-box {{ background: white; padding: 15px; margin: 10px 0; border-left: 4px solid #BAAA49; }}
        .conversation {{ background: white; padding: 15px; margin: 10px 0; border: 1px solid #ddd; max-height: 400px; overflow-y: auto; }}
        .label {{ font-weight: bold; color: #151A2C; }}
        .role-badge {{ display: inline-block; background: #BAAA49; color: #151A2C; padding: 4px 12px; border-radius: 4px; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>🚨 Chatbot Escalation - Ticket #{ticketId}</h2>
        </div>
        <div class='content'>
            <h3>User Support Request</h3>

            <div class='info-box'>
                <p><span class='label'>User Name:</span> {userName}</p>
                <p><span class='label'>User Email:</span> {userEmail}</p>
                <p><span class='label'>User ID:</span> {userId}</p>
                <p><span class='label'>User Role:</span> <span class='role-badge'>{userRole}</span></p>
                <p><span class='label'>Language:</span> {(lang == "zu" ? "isiZulu" : "English")}</p>
                <p><span class='label'>Ticket ID:</span> #{ticketId}</p>
                <p><span class='label'>Date/Time:</span> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
            </div>

            <div class='info-box'>
                <p><span class='label'>Last User Message:</span></p>
                <p>{userMessage}</p>
            </div>

            <div class='conversation'>
                <p><span class='label'>Recent Conversation History:</span></p>
                <pre style='white-space: pre-wrap;'>{conversationHistory}</pre>
            </div>

            <div class='info-box'>
                <p><span class='label'>Action Required:</span></p>
                <p>Please contact the user to assist with their inquiry. They are expecting a response from a human support agent.</p>
                <p><strong>Note:</strong> User role is <strong>{userRole}</strong> - ensure you have appropriate permissions/context when responding.</p>
            </div>
        </div>
    </div>
</body>
</html>";

                var supportEmail = _configuration["SupportEmail"] ?? "support@majoko.local";
                await _emailService.SendEmailAsync(
                    supportEmail,
                    $"Chatbot Escalation - Ticket #{ticketId} - {userName} ({userRole})",
                    emailBody,
                    isHtml: true
                );

                _logger.LogInformation($"Escalation email sent for ticket #{ticketId} - User: {userName} ({userRole})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send escalation email for ticket #{ticketId}");
            }

            return ticketId;
        }
    }
}
