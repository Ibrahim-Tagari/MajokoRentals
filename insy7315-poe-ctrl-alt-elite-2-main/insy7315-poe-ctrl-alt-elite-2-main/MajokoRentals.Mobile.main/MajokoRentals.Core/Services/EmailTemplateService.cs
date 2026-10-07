using System.Text;

namespace MajokoRentals.Core.Services
{
    public interface IEmailTemplateService
    {
        string GetWelcomeEmailTemplate(string firstName, string lastName, string email, string password, string role, string loginUrl);
        string GetLeaseCreatedEmailTemplate(string tenantName, string propertyName, DateTime startDate, DateTime endDate, decimal monthlyRent);
        string GetLeaseRenewedEmailTemplate(string tenantName, string propertyName, DateTime newStartDate, DateTime newEndDate);
        string GetLeaseTerminatedEmailTemplate(string tenantName, string propertyName, DateTime terminationDate);
        string GetPaymentReceivedEmailTemplate(string tenantName, decimal amount, string invoiceNumber, string? transactionRef = null);
        string GetApplicationReceivedEmailTemplate(string firstName, string propertyName, string applicationNumber);
        string GetApplicationApprovedEmailTemplate(string firstName, string propertyName);
        string GetApplicationRejectedEmailTemplate(string firstName, string propertyName, string? reason = null);
        string GetNewApplicationNotificationTemplate(string propertyName, string applicantName, string applicationNumber);
        string GetInvoiceGeneratedEmailTemplate(string tenantName, string propertyName, string invoiceNumber, decimal amount, DateTime dueDate);
        string GetPaymentReminderEmailTemplate(string tenantName, string invoiceNumber, decimal amountDue, DateTime dueDate, bool isOverdue, int daysOverdue = 0);
    }

    public class EmailTemplateService : IEmailTemplateService
    {
        private string GetBaseTemplate(string title, string content)
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

        public string GetWelcomeEmailTemplate(string firstName, string lastName, string email, string password, string role, string loginUrl)
        {
            var content = $@"
                <h2>Welcome to Dr Majoko Projects!</h2>
                <p>Dear <strong>{firstName} {lastName}</strong>,</p>
                <p>Your account has been successfully created by an administrator. We're excited to have you on board!</p>

                <div class='info-box'>
                    <h3>Your Login Credentials</h3>
                    <div class='info-item'><strong>Email:</strong> {email}</div>
                    <div class='info-item'><strong>Temporary Password:</strong> {password}</div>
                    <div class='info-item'><strong>Account Role:</strong> {role}</div>
                </div>

                <div class='alert-box danger'>
                    <strong>⚠️ Important Security Notice:</strong><br>
                    For your security, please change your password immediately after your first login.
                </div>

                <p style='text-align: center;'>
                    <a href='{loginUrl}' class='button'>Login to Your Account</a>
                </p>

                <p>If you have any questions or need assistance, please don't hesitate to contact us.</p>

                <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>";

            return GetBaseTemplate("Welcome to Dr Majoko Projects", content);
        }

        public string GetLeaseCreatedEmailTemplate(string tenantName, string propertyName, DateTime startDate, DateTime endDate, decimal monthlyRent)
        {
            var content = $@"
                <h2>New Lease Agreement Created</h2>
                <p>Dear <strong>{tenantName}</strong>,</p>
                <p>We're pleased to confirm that your lease agreement has been successfully created.</p>

                <div class='info-box'>
                    <h3>Lease Details</h3>
                    <div class='info-item'><strong>Property:</strong> {propertyName}</div>
                    <div class='info-item'><strong>Lease Start Date:</strong> {startDate:dd MMMM yyyy}</div>
                    <div class='info-item'><strong>Lease End Date:</strong> {endDate:dd MMMM yyyy}</div>
                    <div class='info-item'><strong>Monthly Rent:</strong> R {monthlyRent:N2}</div>
                    <div class='info-item'><strong>Lease Duration:</strong> {(endDate - startDate).Days / 30} months</div>
                </div>

                <p>Please review your lease agreement document and ensure all details are correct. If you have any questions or concerns, please contact us immediately.</p>

                <p>We look forward to welcoming you to your new home!</p>

                <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>";

            return GetBaseTemplate("New Lease Agreement", content);
        }

        public string GetLeaseRenewedEmailTemplate(string tenantName, string propertyName, DateTime newStartDate, DateTime newEndDate)
        {
            var content = $@"
                <h2>Lease Renewal Confirmation</h2>
                <p>Dear <strong>{tenantName}</strong>,</p>
                <p>Great news! Your lease has been successfully renewed.</p>

                <div class='info-box'>
                    <h3>Renewed Lease Details</h3>
                    <div class='info-item'><strong>Property:</strong> {propertyName}</div>
                    <div class='info-item'><strong>New Start Date:</strong> {newStartDate:dd MMMM yyyy}</div>
                    <div class='info-item'><strong>New End Date:</strong> {newEndDate:dd MMMM yyyy}</div>
                    <div class='info-item'><strong>Renewal Period:</strong> {(newEndDate - newStartDate).Days / 30} months</div>
                </div>

                <div class='alert-box success'>
                    <strong>✓ Renewal Successful!</strong><br>
                    Your lease has been extended. Thank you for continuing your tenancy with us.
                </div>

                <p>If you have any questions about your renewed lease terms, please don't hesitate to reach out.</p>

                <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>";

            return GetBaseTemplate("Lease Renewal Confirmation", content);
        }

        public string GetLeaseTerminatedEmailTemplate(string tenantName, string propertyName, DateTime terminationDate)
        {
            var content = $@"
                <h2>Lease Termination Notice</h2>
                <p>Dear <strong>{tenantName}</strong>,</p>
                <p>This email confirms that your lease agreement has been terminated.</p>

                <div class='info-box'>
                    <h3>Termination Details</h3>
                    <div class='info-item'><strong>Property:</strong> {propertyName}</div>
                    <div class='info-item'><strong>Termination Date:</strong> {terminationDate:dd MMMM yyyy}</div>
                </div>

                <div class='alert-box'>
                    <strong>Next Steps:</strong><br>
                    Please contact your property manager to discuss:
                    <ul style='margin: 10px 0 0 20px;'>
                        <li>Final move-out inspection arrangements</li>
                        <li>Outstanding payments or deposit refunds</li>
                        <li>Key return procedures</li>
                    </ul>
                </div>

                <p>We appreciate your tenancy and wish you all the best in your future endeavors.</p>

                <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>";

            return GetBaseTemplate("Lease Termination Notice", content);
        }

        public string GetPaymentReceivedEmailTemplate(string tenantName, decimal amount, string invoiceNumber, string? transactionRef = null)
        {
            var content = $@"
                <h2>Payment Received - Thank You!</h2>
                <p>Dear <strong>{tenantName}</strong>,</p>
                <p>We have successfully received your payment. Thank you for your prompt payment!</p>

                <div class='info-box'>
                    <h3>Payment Details</h3>
                    <div class='info-item'><strong>Invoice Number:</strong> {invoiceNumber}</div>
                    <div class='info-item'><strong>Amount Paid:</strong> R {amount:N2}</div>
                    <div class='info-item'><strong>Payment Date:</strong> {DateTime.Today:dd MMMM yyyy}</div>
                    {(string.IsNullOrEmpty(transactionRef) ? "" : $"<div class='info-item'><strong>Transaction Reference:</strong> {transactionRef}</div>")}
                </div>

                <div class='alert-box success'>
                    <strong>✓ Payment Confirmed!</strong><br>
                    Your payment has been recorded and your account has been updated.
                </div>

                <p>You can view your payment history and download receipts by logging into your account.</p>

                <p>Thank you for your continued partnership.</p>

                <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>";

            return GetBaseTemplate("Payment Received", content);
        }

        public string GetApplicationReceivedEmailTemplate(string firstName, string propertyName, string applicationNumber)
        {
            var content = $@"
                <h2>Application Received Successfully</h2>
                <p>Dear <strong>{firstName}</strong>,</p>
                <p>Thank you for submitting your rental application. We have received it and our team is currently reviewing it.</p>

                <div class='info-box'>
                    <h3>Application Details</h3>
                    <div class='info-item'><strong>Property:</strong> {propertyName}</div>
                    <div class='info-item'><strong>Application Number:</strong> {applicationNumber}</div>
                    <div class='info-item'><strong>Submission Date:</strong> {DateTime.Today:dd MMMM yyyy}</div>
                </div>

                <p><strong>What happens next?</strong></p>
                <ul style='color: #555555;'>
                    <li>Our team will review your application within 2-3 business days</li>
                    <li>We may contact you if we need additional information</li>
                    <li>You will receive an email notification once a decision has been made</li>
                </ul>

                <p>We appreciate your patience during the review process.</p>

                <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>";

            return GetBaseTemplate("Application Received", content);
        }

        public string GetApplicationApprovedEmailTemplate(string firstName, string propertyName)
        {
            var content = $@"
                <h2>Congratulations! Application Approved</h2>
                <p>Dear <strong>{firstName}</strong>,</p>
                <p>We are delighted to inform you that your rental application has been <strong>approved</strong>!</p>

                <div class='alert-box success'>
                    <strong>✓ Application Approved!</strong><br>
                    Your application for {propertyName} has been successfully approved.
                </div>

                <div class='info-box'>
                    <h3>Next Steps</h3>
                    <ul style='margin: 10px 0 0 0;'>
                        <li>Our team will contact you within 24-48 hours</li>
                        <li>We will discuss lease agreement details and move-in procedures</li>
                        <li>Required documentation and deposit information will be provided</li>
                        <li>A move-in date will be scheduled</li>
                    </ul>
                </div>

                <p>We look forward to welcoming you as our new tenant!</p>

                <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>";

            return GetBaseTemplate("Application Approved", content);
        }

        public string GetApplicationRejectedEmailTemplate(string firstName, string propertyName, string? reason = null)
        {
            var reasonSection = string.IsNullOrEmpty(reason) ? "" : $@"
                <div class='info-box'>
                    <h3>Additional Information</h3>
                    <p style='margin: 0; color: #555555;'>{reason}</p>
                </div>";

            var content = $@"
                <h2>Application Update</h2>
                <p>Dear <strong>{firstName}</strong>,</p>
                <p>Thank you for your interest in <strong>{propertyName}</strong> and for taking the time to submit a rental application.</p>

                <p>After careful consideration, we regret to inform you that we are unable to approve your application at this time.</p>

                {reasonSection}

                <p>We encourage you to:</p>
                <ul style='color: #555555;'>
                    <li>Check our website for other available properties that may suit your needs</li>
                    <li>Reapply in the future when circumstances change</li>
                    <li>Contact us if you have any questions about this decision</li>
                </ul>

                <p>We appreciate your understanding and wish you the best in finding suitable accommodation.</p>

                <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>";

            return GetBaseTemplate("Application Update", content);
        }

        public string GetNewApplicationNotificationTemplate(string propertyName, string applicantName, string applicationNumber)
        {
            var content = $@"
                <h2>New Rental Application Submitted</h2>
                <p>A new rental application has been submitted and requires your review.</p>

                <div class='info-box'>
                    <h3>Application Summary</h3>
                    <div class='info-item'><strong>Property:</strong> {propertyName}</div>
                    <div class='info-item'><strong>Applicant:</strong> {applicantName}</div>
                    <div class='info-item'><strong>Application Number:</strong> {applicationNumber}</div>
                    <div class='info-item'><strong>Submission Date:</strong> {DateTime.Today:dd MMMM yyyy}</div>
                </div>

                <div class='alert-box'>
                    <strong>Action Required:</strong><br>
                    Please log in to the system to review this application and make a decision.
                </div>

                <p style='text-align: center;'>
                    <a href='#' class='button'>Review Application</a>
                </p>

                <p>Best regards,<br><strong>System Notification</strong></p>";

            return GetBaseTemplate("New Application - Review Required", content);
        }

        public string GetInvoiceGeneratedEmailTemplate(string tenantName, string propertyName, string invoiceNumber, decimal amount, DateTime dueDate)
        {
            var daysUntilDue = (dueDate - DateTime.Today).Days;

            var content = $@"
                <h2>New Invoice Generated</h2>
                <p>Dear <strong>{tenantName}</strong>,</p>
                <p>A new rental invoice has been generated for your property.</p>

                <div class='info-box'>
                    <h3>Invoice Details</h3>
                    <div class='info-item'><strong>Property:</strong> {propertyName}</div>
                    <div class='info-item'><strong>Invoice Number:</strong> {invoiceNumber}</div>
                    <div class='info-item'><strong>Amount Due:</strong> R {amount:N2}</div>
                    <div class='info-item'><strong>Due Date:</strong> {dueDate:dd MMMM yyyy}</div>
                    <div class='info-item'><strong>Days Until Due:</strong> {daysUntilDue} days</div>
                </div>

                <div class='alert-box'>
                    <strong>Payment Reminder:</strong><br>
                    Please ensure payment is made before <strong>{dueDate:dd MMMM yyyy}</strong> to avoid late fees.
                </div>

                <p>You can view and pay your invoice by logging into your account.</p>

                <p>Thank you for your prompt attention to this matter.</p>

                <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>";

            return GetBaseTemplate("New Invoice Generated", content);
        }

        public string GetPaymentReminderEmailTemplate(string tenantName, string invoiceNumber, decimal amountDue, DateTime dueDate, bool isOverdue, int daysOverdue = 0)
        {
            var alertClass = isOverdue ? "danger" : "";
            var alertTitle = isOverdue ? "⚠️ Payment Overdue!" : "Payment Due Soon";
            var statusMessage = isOverdue
                ? $"Your payment is now <strong>{daysOverdue} days overdue</strong>. Please make payment immediately to avoid additional late fees and potential consequences."
                : $"Your payment is due on <strong>{dueDate:dd MMMM yyyy}</strong>. Please ensure timely payment to avoid late fees.";

            var content = $@"
                <h2>Rent Payment Reminder</h2>
                <p>Dear <strong>{tenantName}</strong>,</p>
                <p>This is a reminder regarding your outstanding rent payment.</p>

                <div class='alert-box {alertClass}'>
                    <strong>{alertTitle}</strong><br>
                    {statusMessage}
                </div>

                <div class='info-box'>
                    <h3>Payment Details</h3>
                    <div class='info-item'><strong>Invoice Number:</strong> {invoiceNumber}</div>
                    <div class='info-item'><strong>Amount Due:</strong> R {amountDue:N2}</div>
                    <div class='info-item'><strong>Due Date:</strong> {dueDate:dd MMMM yyyy}</div>
                    {(isOverdue ? $"<div class='info-item'><strong>Days Overdue:</strong> {daysOverdue} days</div>" : "")}
                </div>

                <p><strong>Payment Options:</strong></p>
                <ul style='color: #555555;'>
                    <li>Log in to your account to pay online</li>
                    <li>Contact us for alternative payment arrangements</li>
                </ul>

                <p>If you have already made this payment, please disregard this reminder. If you are experiencing financial difficulties, please contact us to discuss payment options.</p>

                <p>Best regards,<br><strong>Dr Majoko Projects Team</strong></p>";

            return GetBaseTemplate("Payment Reminder", content);
        }
    }
}
