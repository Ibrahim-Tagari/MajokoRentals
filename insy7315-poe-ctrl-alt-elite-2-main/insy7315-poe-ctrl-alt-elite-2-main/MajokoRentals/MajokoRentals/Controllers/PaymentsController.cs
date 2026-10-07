using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using MajokoRentals.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MajokoRentals.Controllers
{
    [Authorize]
    public class PaymentsController : Controller
    {
        private readonly IApiClientService _apiClient;
        private readonly IFileUploadService _fileUploadService;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly IUserProfileService _userProfile;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            IApiClientService apiClient,
            IFileUploadService fileUploadService,
            IEmailService emailService,
            IEmailTemplateService emailTemplateService,
            IUserProfileService userProfile,
            ILogger<PaymentsController> logger)
        {
            _apiClient = apiClient;
            _fileUploadService = fileUploadService;
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
            _userProfile = userProfile;
            _logger = logger;
        }

        private void SetupApiAuthentication()
        {
            var firebaseToken = HttpContext.Session.GetString("FirebaseToken");
            if (!string.IsNullOrEmpty(firebaseToken))
            {
                _apiClient.SetAuthToken(firebaseToken);
            }
        }

        // GET: Payments/Invoices
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> Invoices(PaymentStatus? status, bool? overdue)
        {
            try
            {
                SetupApiAuthentication();

                // Build query string
                var queryParams = new List<string>();
                if (status.HasValue) queryParams.Add($"status={status.Value}");
                if (overdue.HasValue) queryParams.Add($"overdue={overdue.Value}");

                var endpoint = "/api/invoices";
                if (queryParams.Any())
                {
                    endpoint += "?" + string.Join("&", queryParams);
                }

                var invoicesData = await _apiClient.GetAsync<List<InvoiceDto>>(endpoint);

                if (invoicesData == null)
                {
                    invoicesData = new List<InvoiceDto>();
                }

                // Transform to ViewModels
                var invoices = invoicesData.Select(i => new InvoiceListViewModel
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    PropertyName = i.PropertyName,
                    TenantName = i.TenantName,
                    InvoiceDate = i.InvoiceDate,
                    DueDate = i.DueDate,
                    Amount = i.Amount,
                    AmountPaid = i.AmountPaid,
                    AmountDue = i.AmountDue,
                    Status = i.Status,
                    IsOverdue = i.IsOverdue,
                    DaysOverdue = (int)(DateTime.UtcNow - i.DueDate).TotalDays
                }).ToList();

                ViewBag.Status = status;
                ViewBag.Overdue = overdue;

                return View(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoices");
                TempData["ErrorMessage"] = "Unable to load invoices. Please try again.";
                return View(new List<InvoiceListViewModel>());
            }
        }

        // GET: Payments/MyInvoices - For tenants to view their invoices
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> MyInvoices()
        {
            try
            {
                SetupApiAuthentication();

                // API automatically filters for current user
                var invoicesData = await _apiClient.GetAsync<List<InvoiceDto>>("/api/invoices");

                if (invoicesData == null)
                {
                    invoicesData = new List<InvoiceDto>();
                }

                // Transform to ViewModels
                var invoices = invoicesData.Select(i => new InvoiceListViewModel
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    PropertyName = i.PropertyName,
                    TenantName = i.TenantName,
                    InvoiceDate = i.InvoiceDate,
                    DueDate = i.DueDate,
                    Amount = i.Amount,
                    AmountPaid = i.AmountPaid,
                    AmountDue = i.AmountDue,
                    Status = i.Status,
                    IsOverdue = i.IsOverdue,
                    DaysOverdue = (int)(DateTime.UtcNow - i.DueDate).TotalDays
                }).ToList();

                return View("Invoices", invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tenant's invoices");
                return View("Invoices", new List<InvoiceListViewModel>());
            }
        }

        // GET: Payments/InvoiceDetails/5
        [HttpGet("Payments/InvoiceDetails/{id}")]
        public async Task<IActionResult> InvoiceDetails(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            try
            {
                SetupApiAuthentication();

                var invoice = await _apiClient.GetAsync<InvoiceDto>($"/api/invoices/{id}");

                if (invoice == null)
                {
                    return NotFound();
                }

                var viewModel = new InvoiceDetailsViewModel
                {
                    Id = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    LeaseId = invoice.LeaseId,
                    PropertyName = invoice.PropertyName,
                    PropertyAddress = "",
                    TenantName = invoice.TenantName,
                    TenantEmail = "",
                    InvoiceDate = invoice.InvoiceDate,
                    DueDate = invoice.DueDate,
                    Amount = invoice.Amount,
                    AmountPaid = invoice.AmountPaid,
                    AmountDue = invoice.AmountDue,
                    Status = invoice.Status,
                    Description = "",
                    Notes = "",
                    PaidDate = null,
                    IsOverdue = invoice.IsOverdue,
                    DaysOverdue = (int)(DateTime.UtcNow - invoice.DueDate).TotalDays,
                    Payments = new List<Payment>()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading invoice {id}");
                return NotFound();
            }
        }

        // GET: Payments/RecordPayment/5
        [HttpGet("Payments/RecordPayment/{id}")]
        [Authorize(Roles = "Admin,PropertyManager,Tenant")]
        public async Task<IActionResult> RecordPayment(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            try
            {
                SetupApiAuthentication();

                var invoice = await _apiClient.GetAsync<InvoiceDto>($"/api/invoices/{id}");

                if (invoice == null)
                {
                    return NotFound();
                }

                var viewModel = new RecordPaymentViewModel
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceAmount = invoice.Amount,
                    AmountAlreadyPaid = invoice.AmountPaid,
                    AmountDue = invoice.AmountDue,
                    Amount = invoice.AmountDue,
                    PaymentDate = DateTime.Today
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading invoice {id} for payment");
                return NotFound();
            }
        }

        // POST: Payments/RecordPayment
        [HttpPost("Payments/RecordPayment/{id}")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager,Tenant")]
        public async Task<IActionResult> RecordPayment(string id, RecordPaymentViewModel viewModel)
        {
            if (id != viewModel.InvoiceId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    SetupApiAuthentication();

                    // Get invoice to validate payment amount
                    var invoice = await _apiClient.GetAsync<InvoiceDto>($"/api/invoices/{id}");
                    if (invoice == null)
                    {
                        return NotFound();
                    }

                    // Validate payment amount
                    if (viewModel.Amount > invoice.AmountDue)
                    {
                        ModelState.AddModelError("Amount", "Payment amount cannot exceed the amount due.");
                        return View(viewModel);
                    }

                    // Upload payment proof if provided
                    string? proofUrl = null;
                    if (viewModel.PaymentProof != null)
                    {
                        proofUrl = await _fileUploadService.UploadFileAsync(viewModel.PaymentProof, "payments");
                    }

                    // Create payment record via API
                    var recordPaymentDto = new RecordPaymentDto
                    {
                        InvoiceId = invoice.Id,
                        Amount = viewModel.Amount,
                        PaymentDate = viewModel.PaymentDate,
                        PaymentMethod = viewModel.PaymentMethod.ToString(),
                        ReferenceNumber = viewModel.TransactionReference
                    };

                    var response = await _apiClient.PostAsync<RecordPaymentDto, object>("/api/invoices/payments", recordPaymentDto);

                    if (response != null)
                    {
                        // Send confirmation email
                        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                        var tenant = userId != null ? await _userProfile.GetUserProfileAsync(userId) : null;
                        if (tenant?.Email != null)
                        {
                            var emailBody = _emailTemplateService.GetPaymentReceivedEmailTemplate(
                                $"{tenant.FirstName} {tenant.LastName}",
                                (decimal)viewModel.Amount,
                                invoice.InvoiceNumber,
                                viewModel.TransactionReference
                            );

                            await _emailService.SendEmailAsync(
                                tenant.Email,
                                "Payment Received",
                                emailBody,
                                isHtml: true
                            );
                        }

                        TempData["SuccessMessage"] = "Payment recorded successfully!";
                        return RedirectToAction(nameof(InvoiceDetails), new { id = invoice.Id });
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to record payment. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error recording payment");
                    ModelState.AddModelError("", "An error occurred while recording the payment.");
                }
            }

            return View(viewModel);
        }

        // GET: Payments/GenerateInvoices
        [Authorize(Roles = "Admin,PropertyManager")]
        public IActionResult GenerateInvoices()
        {
            var viewModel = new GenerateInvoicesViewModel();
            return View(viewModel);
        }

        // POST: Payments/GenerateInvoices
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> GenerateInvoices(GenerateInvoicesViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    SetupApiAuthentication();

                    var generateDto = new GenerateInvoicesDto
                    {
                        BillingMonth = viewModel.BillingMonth,
                        InvoiceDate = viewModel.InvoiceDate,
                        DueDate = viewModel.DueDate,
                        ActiveLeasesOnly = viewModel.ActiveLeasesOnly
                    };

                    var response = await _apiClient.PostAsync<GenerateInvoicesDto, GenerateInvoicesResponseDto>("/api/invoices/generate", generateDto);

                    if (response != null)
                    {
                        TempData["SuccessMessage"] = response.Message;
                        return RedirectToAction(nameof(Invoices));
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to generate invoices. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating invoices");
                    ModelState.AddModelError("", "An error occurred while generating invoices.");
                }
            }

            return View(viewModel);
        }

        // GET: Payments/PaymentHistory - For property managers to see all payments
        [Authorize(Roles = "Admin,PropertyManager")]
        public async Task<IActionResult> PaymentHistory(int? leaseId)
        {
            try
            {
                SetupApiAuthentication();

                // Get all invoices with their payments
                var invoices = await _apiClient.GetAsync<List<InvoiceDto>>("/api/invoices");

                if (invoices == null)
                {
                    return View(new List<PaymentDto>());
                }

                // Extract all payments from invoices
                var allPayments = invoices
                    .SelectMany(i => i.Payments ?? new List<PaymentDto>())
                    .OrderByDescending(p => p.PaymentDate)
                    .ToList();

                return View(allPayments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payment history");
                TempData["ErrorMessage"] = "Unable to load payment history. Please try again.";
                return View(new List<PaymentDto>());
            }
        }

        // GET: Payments/PaymentGateway - Mock payment gateway
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> PaymentGateway(string id)
        {
            try
            {
                SetupApiAuthentication();

                // Get invoice details
                var invoice = await _apiClient.GetAsync<InvoiceDto>($"/api/invoices/{id}");

                if (invoice == null)
                {
                    TempData["ErrorMessage"] = "Invoice not found.";
                    return RedirectToAction(nameof(MyInvoices));
                }

                ViewBag.Invoice = invoice;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payment gateway");
                TempData["ErrorMessage"] = "Unable to load payment gateway. Please try again.";
                return RedirectToAction(nameof(MyInvoices));
            }
        }

        // POST: Payments/ProcessPayment - Process mock payment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> ProcessPayment(string invoiceId)
        {
            try
            {
                SetupApiAuthentication();

                // Get invoice details
                var invoice = await _apiClient.GetAsync<InvoiceDto>($"/api/invoices/{invoiceId}");
                if (invoice == null)
                {
                    TempData["ErrorMessage"] = "Invoice not found.";
                    return RedirectToAction(nameof(MyInvoices));
                }

                // Simulate payment processing time
                await Task.Delay(1500);

                // Generate mock transaction reference
                var transactionRef = $"MOCK-{DateTime.Now:yyyyMMddHHmmss}-{new Random().Next(1000, 9999)}";

                // Create payment record via API
                var recordPaymentDto = new RecordPaymentDto
                {
                    InvoiceId = invoice.Id,
                    Amount = invoice.AmountDue, // Pay the full amount due
                    PaymentDate = DateTime.Today,
                    PaymentMethod = "Card", // Credit/Debit card payment
                    ReferenceNumber = transactionRef
                };

                var response = await _apiClient.PostAsync<RecordPaymentDto, object>("/api/invoices/payments", recordPaymentDto);

                if (response != null)
                {
                    // Send confirmation email
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var tenant = userId != null ? await _userProfile.GetUserProfileAsync(userId) : null;
                    if (tenant?.Email != null)
                    {
                        var emailBody = _emailTemplateService.GetPaymentReceivedEmailTemplate(
                            $"{tenant.FirstName} {tenant.LastName}",
                            (decimal)invoice.AmountDue,
                            invoice.InvoiceNumber,
                            transactionRef
                        );

                        await _emailService.SendEmailAsync(
                            tenant.Email,
                            "Payment Received",
                            emailBody,
                            isHtml: true
                        );
                    }

                    TempData["SuccessMessage"] = $"Payment processed successfully! Your payment of R {invoice.AmountDue:N2} has been recorded. Transaction Reference: {transactionRef}";
                    return RedirectToAction(nameof(MyInvoices));
                }
                else
                {
                    TempData["ErrorMessage"] = "Payment processing failed. Please try again.";
                    return RedirectToAction(nameof(PaymentGateway), new { id = invoiceId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment");
                TempData["ErrorMessage"] = "Payment processing failed. Please try again.";
                return RedirectToAction(nameof(PaymentGateway), new { id = invoiceId });
            }
        }
    }

    // DTOs for API calls
    public class InvoiceDto
    {
        public string Id { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public long LeaseId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public double Amount { get; set; }
        public double AmountPaid { get; set; }
        public double AmountDue { get; set; }
        public PaymentStatus Status { get; set; }
        public bool IsOverdue { get; set; }
        public List<PaymentDto>? Payments { get; set; }
    }

    public class PaymentDto
    {
        public long Id { get; set; }
        public long InvoiceId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public double Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string? ProofOfPaymentUrl { get; set; }
    }

    public class RecordPaymentDto
    {
        public string InvoiceId { get; set; } = string.Empty;
        public double Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
    }

    public class GenerateInvoicesDto
    {
        public DateTime BillingMonth { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public bool ActiveLeasesOnly { get; set; }
    }

    public class GenerateInvoicesResponseDto
    {
        public int Count { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
