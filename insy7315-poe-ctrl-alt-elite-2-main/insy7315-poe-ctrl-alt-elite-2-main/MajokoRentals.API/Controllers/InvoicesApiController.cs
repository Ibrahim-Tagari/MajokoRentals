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
    public class InvoicesController : ControllerBase
    {
        private readonly IFirestoreService _firestore;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly ILogger<InvoicesController> _logger;

        public InvoicesController(
            IFirestoreService firestore,
            IEmailService emailService,
            IEmailTemplateService emailTemplateService,
            ILogger<InvoicesController> logger)
        {
            _firestore = firestore;
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
            _logger = logger;
        }

        /// <summary>
        /// Get current user's invoices (tenant-specific)
        /// </summary>
        [HttpGet("my")]
        [Authorize(Roles = "Tenant")]
        [ProducesResponseType(typeof(IEnumerable<InvoiceDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyInvoices()
        {
            // Redirect to main GetInvoices endpoint which already filters by tenant
            return await GetInvoices(null, null);
        }

        /// <summary>
        /// Get all invoices (filtered by role)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<InvoiceDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetInvoices(
            [FromQuery] PaymentStatus? status = null,
            [FromQuery] bool? overdue = null)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var allInvoices = await _firestore.GetCollectionAsync<Dictionary<string, object>>("invoices");

                // Filter by role
                var filteredInvoices = allInvoices.Where(i =>
                {
                    // Tenant filtering
                    if (User.IsInRole("Tenant") && i.ContainsKey("tenantId"))
                    {
                        if (i["tenantId"].ToString() != userId) return false;
                    }

                    // Status filter
                    if (status.HasValue && i.ContainsKey("status"))
                    {
                        var invStatus = Enum.Parse<PaymentStatus>(i["status"].ToString()!);
                        if (invStatus != status.Value) return false;
                    }

                    // Overdue filter
                    if (overdue.HasValue && overdue.Value && i.ContainsKey("dueDate") && i.ContainsKey("status"))
                    {
                        var dueDate = DateTime.Parse(i["dueDate"].ToString()!);
                        var invStatus = Enum.Parse<PaymentStatus>(i["status"].ToString()!);
                        if (!(dueDate < DateTime.UtcNow && invStatus != PaymentStatus.Paid)) return false;
                    }

                    return true;
                });

                // Get payments for these invoices
                var allPayments = await _firestore.GetCollectionAsync<Dictionary<string, object>>("payments");

                var invoices = filteredInvoices
                    .OrderByDescending(i => i.ContainsKey("invoiceDate") ? DateTime.Parse(i["invoiceDate"].ToString()!) : DateTime.MinValue)
                    .Select(i =>
                    {
                        var firestoreDocId = i.ContainsKey("uid") ? i["uid"].ToString()! : "";
                        var invoiceId = i.ContainsKey("id") ? Convert.ToInt64(i["id"]) : 0L;
                        var payments = allPayments
                            .Where(p => p.ContainsKey("invoiceId") && Convert.ToInt64(p["invoiceId"]) == invoiceId)
                            .Select(p => new PaymentDto
                            {
                                Id = p.ContainsKey("id") ? Convert.ToInt64(p["id"]) : 0L,
                                InvoiceId = p.ContainsKey("invoiceId") ? Convert.ToInt64(p["invoiceId"]) : 0L,
                                TenantName = i.ContainsKey("tenantName") ? i["tenantName"].ToString() : "",
                                Amount = p.ContainsKey("amount") ? Convert.ToDouble(p["amount"]) : 0,
                                PaymentDate = p.ContainsKey("paymentDate") ? DateTime.Parse(p["paymentDate"].ToString()!) : DateTime.UtcNow,
                                PaymentMethod = p.ContainsKey("paymentMethod") ? p["paymentMethod"].ToString()! : PaymentMethod.Cash.ToString(),
                                ReferenceNumber = p.ContainsKey("referenceNumber") ? p["referenceNumber"].ToString() : "",
                                ProofOfPaymentUrl = p.ContainsKey("proofOfPaymentUrl") ? p["proofOfPaymentUrl"].ToString() : null
                            })
                            .ToList();

                        var amount = i.ContainsKey("amount") ? Convert.ToDouble(i["amount"]) : 0;
                        var amountPaid = i.ContainsKey("amountPaid") ? Convert.ToDouble(i["amountPaid"]) : 0;
                        var dueDate = i.ContainsKey("dueDate") ? DateTime.Parse(i["dueDate"].ToString()!) : DateTime.UtcNow;
                        var invStatus = i.ContainsKey("status") ? Enum.Parse<PaymentStatus>(i["status"].ToString()!) : PaymentStatus.Pending;

                        return new InvoiceDto
                        {
                            Id = firestoreDocId,
                            InvoiceNumber = i.ContainsKey("invoiceNumber") ? i["invoiceNumber"].ToString()! : "",
                            LeaseId = i.ContainsKey("leaseId") ? Convert.ToInt64(i["leaseId"]) : 0L,
                            PropertyName = i.ContainsKey("propertyName") ? i["propertyName"].ToString()! : "",
                            TenantName = i.ContainsKey("tenantName") ? i["tenantName"].ToString()! : "",
                            InvoiceDate = i.ContainsKey("invoiceDate") ? DateTime.Parse(i["invoiceDate"].ToString()!) : DateTime.UtcNow,
                            DueDate = dueDate,
                            Amount = amount,
                            AmountPaid = amountPaid,
                            AmountDue = amount - amountPaid,
                            Status = invStatus,
                            IsOverdue = dueDate < DateTime.UtcNow && invStatus != PaymentStatus.Paid,
                            Payments = payments
                        };
                    })
                    .ToList();

                return Ok(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoices from Firestore");
                return StatusCode(500, new { message = "Error fetching invoices" });
            }
        }

        /// <summary>
        /// Get a specific invoice
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetInvoice(string id)
        {
            try
            {
                var invoice = await _firestore.GetDocumentAsync<Dictionary<string, object>>("invoices", id);
                if (invoice == null)
                    return NotFound();

                // Authorization check
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!User.IsInRole("Admin") && !User.IsInRole("PropertyManager"))
                {
                    var tenantId = invoice.ContainsKey("tenantId") ? invoice["tenantId"].ToString() : "";
                    if (tenantId != userId)
                        return Forbid();
                }

                // Get payments for this invoice
                var invoiceId = invoice.ContainsKey("id") ? Convert.ToInt64(invoice["id"]) : 0L;
                var allPayments = await _firestore.GetCollectionAsync<Dictionary<string, object>>("payments");
                var payments = allPayments
                    .Where(p => p.ContainsKey("invoiceId") && Convert.ToInt64(p["invoiceId"]) == invoiceId)
                    .Select(p => new PaymentDto
                    {
                        Id = p.ContainsKey("id") ? Convert.ToInt64(p["id"]) : 0L,
                        InvoiceId = p.ContainsKey("invoiceId") ? Convert.ToInt64(p["invoiceId"]) : 0L,
                        TenantName = invoice.ContainsKey("tenantName") ? invoice["tenantName"].ToString() : "",
                        Amount = p.ContainsKey("amount") ? Convert.ToDouble(p["amount"]) : 0,
                        PaymentDate = p.ContainsKey("paymentDate") ? DateTime.Parse(p["paymentDate"].ToString()!) : DateTime.UtcNow,
                        PaymentMethod = p.ContainsKey("paymentMethod") ? p["paymentMethod"].ToString()! : PaymentMethod.Cash.ToString(),
                        ReferenceNumber = p.ContainsKey("referenceNumber") ? p["referenceNumber"].ToString() : "",
                        ProofOfPaymentUrl = p.ContainsKey("proofOfPaymentUrl") ? p["proofOfPaymentUrl"].ToString() : null
                    })
                    .ToList();

                var amount = invoice.ContainsKey("amount") ? Convert.ToDouble(invoice["amount"]) : 0;
                var amountPaid = invoice.ContainsKey("amountPaid") ? Convert.ToDouble(invoice["amountPaid"]) : 0;
                var dueDate = invoice.ContainsKey("dueDate") ? DateTime.Parse(invoice["dueDate"].ToString()!) : DateTime.UtcNow;
                var status = invoice.ContainsKey("status") ? Enum.Parse<PaymentStatus>(invoice["status"].ToString()!) : PaymentStatus.Pending;

                var dto = new InvoiceDto
                {
                    Id = id,  // Use Firestore document ID instead of timestamp ID
                    InvoiceNumber = invoice.ContainsKey("invoiceNumber") ? invoice["invoiceNumber"].ToString()! : "",
                    LeaseId = invoice.ContainsKey("leaseId") ? Convert.ToInt64(invoice["leaseId"]) : 0L,
                    PropertyName = invoice.ContainsKey("propertyName") ? invoice["propertyName"].ToString()! : "",
                    TenantName = invoice.ContainsKey("tenantName") ? invoice["tenantName"].ToString()! : "",
                    InvoiceDate = invoice.ContainsKey("invoiceDate") ? DateTime.Parse(invoice["invoiceDate"].ToString()!) : DateTime.UtcNow,
                    DueDate = dueDate,
                    Amount = amount,
                    AmountPaid = amountPaid,
                    AmountDue = amount - amountPaid,
                    Status = status,
                    IsOverdue = dueDate < DateTime.UtcNow && status != PaymentStatus.Paid,
                    Payments = payments
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching invoice {id} from Firestore");
                return StatusCode(500, new { message = "Error fetching invoice" });
            }
        }

        /// <summary>
        /// Generate invoices for active leases
        /// </summary>
        [HttpPost("generate")]
        [Authorize(Roles = "Admin,PropertyManager")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GenerateInvoices([FromBody] GenerateInvoicesDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // Get all leases
                var allLeases = await _firestore.GetCollectionAsync<Dictionary<string, object>>("leases");
                var allInvoices = await _firestore.GetCollectionAsync<Dictionary<string, object>>("invoices");

                var leasesToBill = allLeases.Where(l =>
                {
                    // Filter by active leases only if specified
                    if (dto.ActiveLeasesOnly && l.ContainsKey("status"))
                    {
                        var status = l["status"].ToString();
                        if (status != LeaseStatus.Active.ToString())
                            return false;
                    }

                    // Check if invoice already exists for this billing month
                    var leaseId = l.ContainsKey("id") ? Convert.ToInt64(l["id"]) : 0;
                    var billingMonth = new DateTime(dto.BillingMonth.Year, dto.BillingMonth.Month, 1);

                    var hasInvoice = allInvoices.Any(inv =>
                    {
                        if (!inv.ContainsKey("leaseId") || !inv.ContainsKey("invoiceDate"))
                            return false;

                        var invLeaseId = Convert.ToInt64(inv["leaseId"]);
                        var invDate = DateTime.Parse(inv["invoiceDate"].ToString()!);
                        var invMonth = new DateTime(invDate.Year, invDate.Month, 1);

                        return invLeaseId == leaseId && invMonth == billingMonth;
                    });

                    return !hasInvoice;
                }).ToList();

                int generatedCount = 0;

                foreach (var lease in leasesToBill)
                {
                    var invoiceId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    await Task.Delay(2); // Ensure unique IDs

                    var monthlyRent = lease.ContainsKey("monthlyRent") ? Convert.ToDouble(lease["monthlyRent"]) : 0;

                    var invoiceData = new Dictionary<string, object>
                    {
                        { "id", invoiceId },
                        { "invoiceNumber", $"INV-{invoiceId}" },
                        { "leaseId", lease.ContainsKey("id") ? lease["id"] : 0 },
                        { "propertyName", lease.ContainsKey("propertyName") ? lease["propertyName"] : "" },
                        { "propertyAddress", lease.ContainsKey("propertyAddress") ? lease["propertyAddress"] : "" },
                        { "tenantId", lease.ContainsKey("tenantId") ? lease["tenantId"] : "" },
                        { "tenantName", lease.ContainsKey("tenantName") ? lease["tenantName"] : "" },
                        { "tenantEmail", lease.ContainsKey("tenantEmail") ? lease["tenantEmail"] : "" },
                        { "invoiceDate", dto.InvoiceDate.ToString("o") },
                        { "dueDate", dto.DueDate.ToString("o") },
                        { "amount", monthlyRent },
                        { "amountPaid", 0.0 },
                        { "status", PaymentStatus.Pending.ToString() },
                        { "description", $"Rent for {dto.BillingMonth:MMMM yyyy}" },
                        { "createdAt", DateTime.UtcNow.ToString("o") },
                        { "updatedAt", DateTime.UtcNow.ToString("o") }
                    };

                    await _firestore.AddDocumentAsync("invoices", invoiceData);
                    generatedCount++;

                    // Send email notification to tenant using professional template
                    try
                    {
                        var tenantEmail = lease.ContainsKey("tenantEmail") ? lease["tenantEmail"]?.ToString() : "";
                        var tenantName = lease.ContainsKey("tenantName") ? lease["tenantName"]?.ToString() : "";
                        var propertyName = lease.ContainsKey("propertyName") ? lease["propertyName"]?.ToString() : "";

                        if (!string.IsNullOrEmpty(tenantEmail) && !string.IsNullOrEmpty(tenantName) && !string.IsNullOrEmpty(propertyName))
                        {
                            var emailSubject = "New Invoice Generated";
                            var emailBody = _emailTemplateService.GetInvoiceGeneratedEmailTemplate(
                                tenantName,
                                propertyName,
                                $"INV-{invoiceId}",
                                (decimal)monthlyRent,
                                dto.DueDate
                            );

                            await _emailService.SendEmailAsync(tenantEmail, emailSubject, emailBody, isHtml: true);
                            _logger.LogInformation($"Invoice email sent to {tenantEmail}");
                        }
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogWarning(emailEx, $"Failed to send invoice email for invoice {invoiceId}");
                        // Continue processing even if email fails
                    }
                }

                _logger.LogInformation($"Generated {generatedCount} invoices");

                return Ok(new { count = generatedCount, message = $"Successfully generated {generatedCount} invoices" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoices");
                return StatusCode(500, new { message = "Error generating invoices" });
            }
        }

        /// <summary>
        /// Record a payment for an invoice
        /// </summary>
        [HttpPost("payments")]
        [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RecordPayment([FromBody] RecordPaymentDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // Get the invoice by Firestore document ID
                var invoice = await _firestore.GetDocumentAsync<Dictionary<string, object>>("invoices", dto.InvoiceId);

                if (invoice == null)
                    return NotFound("Invoice not found");

                // Authorization check
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!User.IsInRole("Admin") && !User.IsInRole("PropertyManager"))
                {
                    var tenantId = invoice.ContainsKey("tenantId") ? invoice["tenantId"].ToString() : "";
                    if (tenantId != userId)
                        return Forbid();
                }

                var paymentId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Get timestamp ID for payment tracking
                var invoiceTimestampId = invoice.ContainsKey("id") ? Convert.ToInt64(invoice["id"]) : 0L;

                // Create payment
                var paymentData = new Dictionary<string, object>
                {
                    { "id", paymentId },
                    { "invoiceId", invoiceTimestampId },  // Use timestamp ID for payment tracking
                    { "tenantId", invoice.ContainsKey("tenantId") ? invoice["tenantId"]! : userId! },
                    { "amount", dto.Amount },
                    { "paymentDate", dto.PaymentDate.ToString("o") },
                    { "paymentMethod", dto.PaymentMethod.ToString() },
                    { "referenceNumber", dto.ReferenceNumber ?? "" },
                    { "proofOfPaymentUrl", "" },
                    { "createdAt", DateTime.UtcNow.ToString("o") }
                };

                var paymentDocId = await _firestore.AddDocumentAsync("payments", paymentData);

                // Update invoice amounts
                var currentAmountPaid = invoice.ContainsKey("amountPaid") ? Convert.ToDouble(invoice["amountPaid"]) : 0;
                var totalAmount = invoice.ContainsKey("amount") ? Convert.ToDouble(invoice["amount"]) : 0;
                var newAmountPaid = currentAmountPaid + dto.Amount;

                invoice["amountPaid"] = newAmountPaid;

                if (newAmountPaid >= totalAmount)
                {
                    invoice["status"] = PaymentStatus.Paid.ToString();
                }
                else if (newAmountPaid > 0)
                {
                    invoice["status"] = PaymentStatus.PartiallyPaid.ToString();
                }

                invoice["updatedAt"] = DateTime.UtcNow.ToString("o");

                // Update the invoice in Firestore using the document ID
                await _firestore.UpdateDocumentAsync("invoices", dto.InvoiceId, invoice);

                _logger.LogInformation($"Payment recorded for invoice {dto.InvoiceId}");

                // Send payment confirmation email to tenant
                try
                {
                    var tenantEmail = invoice.ContainsKey("tenantEmail") ? invoice["tenantEmail"]?.ToString() : "";
                    var tenantName = invoice.ContainsKey("tenantName") ? invoice["tenantName"]?.ToString() : "";
                    var invoiceNumber = invoice.ContainsKey("invoiceNumber") ? invoice["invoiceNumber"]?.ToString() : "";

                    if (!string.IsNullOrEmpty(tenantEmail) && !string.IsNullOrEmpty(tenantName))
                    {
                        var emailSubject = "Payment Received - Thank You!";
                        var emailBody = _emailTemplateService.GetPaymentReceivedEmailTemplate(
                            tenantName,
                            (decimal)dto.Amount,
                            invoiceNumber ?? dto.InvoiceId,
                            dto.ReferenceNumber
                        );

                        await _emailService.SendEmailAsync(tenantEmail, emailSubject, emailBody, isHtml: true);
                        _logger.LogInformation($"Payment confirmation email sent to {tenantEmail}");
                    }
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, $"Failed to send payment confirmation email for invoice {dto.InvoiceId}");
                    // Continue processing even if email fails
                }

                var paymentDto = new PaymentDto
                {
                    Id = paymentId,
                    InvoiceId = invoiceTimestampId,
                    TenantName = invoice.ContainsKey("tenantName") ? invoice["tenantName"].ToString()! : "",
                    Amount = dto.Amount,
                    PaymentDate = dto.PaymentDate,
                    PaymentMethod = dto.PaymentMethod,
                    ReferenceNumber = dto.ReferenceNumber
                };

                return CreatedAtAction(nameof(GetInvoice), new { id = dto.InvoiceId }, paymentDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording payment in Firestore");
                return StatusCode(500, new { message = "Error recording payment" });
            }
        }

        /// <summary>
        /// Get payment statistics for current user
        /// </summary>
        [HttpGet("my-stats")]
        [Authorize(Roles = "Tenant")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyPaymentStats()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var allInvoices = await _firestore.GetCollectionAsync<Dictionary<string, object>>("invoices");
                var myInvoices = allInvoices.Where(i =>
                    i.ContainsKey("tenantId") && i["tenantId"].ToString() == userId &&
                    i.ContainsKey("status") && Enum.Parse<PaymentStatus>(i["status"].ToString()!) != PaymentStatus.Paid
                ).ToList();

                var totalOutstanding = myInvoices.Sum(i =>
                {
                    var amount = i.ContainsKey("amount") ? Convert.ToDouble(i["amount"]) : 0;
                    var amountPaid = i.ContainsKey("amountPaid") ? Convert.ToDouble(i["amountPaid"]) : 0;
                    return amount - amountPaid;
                });

                var overdueCount = myInvoices.Count(i =>
                    i.ContainsKey("dueDate") && DateTime.Parse(i["dueDate"].ToString()!) < DateTime.UtcNow
                );

                var firstDayOfYear = new DateTime(DateTime.UtcNow.Year, 1, 1);
                var allPayments = await _firestore.GetCollectionAsync<Dictionary<string, object>>("payments");
                var myPayments = allPayments.Where(p =>
                    p.ContainsKey("tenantId") && p["tenantId"].ToString() == userId &&
                    p.ContainsKey("paymentDate") && DateTime.Parse(p["paymentDate"].ToString()!) >= firstDayOfYear
                ).ToList();

                var totalPaidThisYear = myPayments.Sum(p =>
                    p.ContainsKey("amount") ? Convert.ToDouble(p["amount"]) : 0
                );

                return Ok(new
                {
                    totalOutstanding,
                    unpaidInvoicesCount = myInvoices.Count,
                    overdueInvoicesCount = overdueCount,
                    totalPaidThisYear
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching payment stats from Firestore");
                return StatusCode(500, new { message = "Error fetching payment stats" });
            }
        }
    }

    // DTOs for Invoice operations
    public class GenerateInvoicesDto
    {
        public DateTime BillingMonth { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public bool ActiveLeasesOnly { get; set; } = true;
    }
}
