using Microsoft.EntityFrameworkCore;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using MajokoRentals.Mobile.Data;
using MajokoRentals.Mobile.Data.LocalModels;
using System.Text.Json;

namespace MajokoRentals.Mobile.main.Services
{
    /// <summary>
    /// Service for synchronizing offline data with the server
    /// </summary>
    public class SyncService : ISyncService
    {
        private readonly LocalDbContext _localDb;
        private readonly IApiClientService _apiClient;
        private readonly IOfflineFileService _fileService;
        private readonly IAuthenticationService _authService;
        private const string LastSyncKey = "last_sync_time";

        public event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;
        public event EventHandler<SyncResult>? SyncCompleted;

        public SyncService(
            LocalDbContext localDb,
            IApiClientService apiClient,
            IOfflineFileService fileService,
            IAuthenticationService authService)
        {
            _localDb = localDb;
            _apiClient = apiClient;
            _fileService = fileService;
            _authService = authService;
        }

        public async Task<SyncResult> SyncAllAsync()
        {
            var result = new SyncResult { Success = true };

            try
            {
                OnSyncProgress("Starting synchronization...", 0, 0);

                // Sync maintenance requests
                var maintenanceResult = await SyncMaintenanceRequestsAsync();
                result.ItemsSynced += maintenanceResult.ItemsSynced;
                result.ItemsFailed += maintenanceResult.ItemsFailed;
                result.Errors.AddRange(maintenanceResult.Errors);
                result.Conflicts.AddRange(maintenanceResult.Conflicts);

                // Sync payments
                var paymentResult = await SyncPaymentsAsync();
                result.ItemsSynced += paymentResult.ItemsSynced;
                result.ItemsFailed += paymentResult.ItemsFailed;
                result.Errors.AddRange(paymentResult.Errors);
                result.Conflicts.AddRange(paymentResult.Conflicts);

                result.Success = result.ItemsFailed == 0;

                // Update last sync time
                await SaveLastSyncTimeAsync(DateTime.UtcNow);

                OnSyncProgress("Synchronization completed", result.ItemsSynced, result.ItemsSynced);
                OnSyncCompleted(result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Sync failed: {ex.Message}");
            }

            return result;
        }

        public async Task<SyncResult> SyncMaintenanceRequestsAsync()
        {
            var result = new SyncResult { Success = true };

            try
            {
                // Get all unsynced maintenance requests
                var unsyncedRequests = await _localDb.MaintenanceRequests
                    .Where(m => !m.IsSynced)
                    .ToListAsync();

                var total = unsyncedRequests.Count;
                var processed = 0;

                OnSyncProgress($"Syncing {total} maintenance request(s)...", total, processed);

                foreach (var request in unsyncedRequests)
                {
                    try
                    {
                        // Upload images first if they exist locally
                        if (!string.IsNullOrEmpty(request.LocalImagePaths))
                        {
                            var imagePaths = JsonSerializer.Deserialize<List<string>>(request.LocalImagePaths) ?? new();
                            var uploadedUrls = new List<string>();

                            foreach (var localPath in imagePaths)
                            {
                                var url = await UploadFileAsync(localPath, "maintenance");
                                if (!string.IsNullOrEmpty(url))
                                {
                                    uploadedUrls.Add(url);
                                }
                            }

                            if (uploadedUrls.Any())
                            {
                                request.ImageUrls = JsonSerializer.Serialize(uploadedUrls);
                            }
                        }

                        // Create DTO for API call
                        var createDto = new CreateMaintenanceRequestDto
                        {
                            PropertyId = request.PropertyId,
                            Title = request.Title,
                            Description = request.Description,
                            Category = request.Category,
                            Priority = request.Priority,
                            Location = request.Location
                        };

                        // Post to server
                        var response = await _apiClient.PostAsync<CreateMaintenanceRequestDto, MaintenanceRequestDto>(
                            "/api/maintenance",
                            createDto
                        );

                        if (response != null)
                        {
                            // Update local record
                            request.IsSynced = true;
                            request.LastSyncedAt = DateTime.UtcNow;
                            request.ServerLastModified = DateTime.UtcNow;

                            // Parse server ID and update
                            if (int.TryParse(response.Id, out int serverId))
                            {
                                request.Id = serverId;
                            }

                            result.ItemsSynced++;
                        }
                        else
                        {
                            result.ItemsFailed++;
                            result.Errors.Add($"Failed to sync maintenance request: {request.Title}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.ItemsFailed++;
                        result.Errors.Add($"Error syncing maintenance request {request.Title}: {ex.Message}");
                    }

                    processed++;
                    OnSyncProgress($"Syncing maintenance requests...", total, processed);
                }

                await _localDb.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Maintenance sync failed: {ex.Message}");
            }

            return result;
        }

        public async Task<SyncResult> SyncPaymentsAsync()
        {
            var result = new SyncResult { Success = true };

            try
            {
                // Get all unsynced payments
                var unsyncedPayments = await _localDb.Payments
                    .Where(p => !p.IsSynced)
                    .ToListAsync();

                var total = unsyncedPayments.Count;
                var processed = 0;

                OnSyncProgress($"Syncing {total} payment(s)...", total, processed);

                foreach (var payment in unsyncedPayments)
                {
                    try
                    {
                        // Upload proof of payment if exists locally
                        if (!string.IsNullOrEmpty(payment.LocalProofOfPaymentPath))
                        {
                            var url = await UploadFileAsync(payment.LocalProofOfPaymentPath, "payment");
                            if (!string.IsNullOrEmpty(url))
                            {
                                payment.ProofOfPaymentUrl = url;
                            }
                        }

                        // Create DTO for API call
                        var recordDto = new RecordPaymentDto
                        {
                            InvoiceId = payment.InvoiceId.ToString(),
                            Amount = payment.Amount,
                            PaymentDate = payment.PaymentDate,
                            PaymentMethod = payment.PaymentMethod,
                            ReferenceNumber = payment.ReferenceNumber,
                            Notes = payment.Notes
                        };

                        // TODO: If proof of payment URL exists, handle it separately
                        // May need to call a different endpoint or add to notes
                        if (!string.IsNullOrEmpty(payment.ProofOfPaymentUrl))
                        {
                            recordDto.Notes = $"{recordDto.Notes}\nProof of Payment: {payment.ProofOfPaymentUrl}".Trim();
                        }

                        // Post to server
                        var response = await _apiClient.PostAsync<RecordPaymentDto, object>(
                            "/api/invoices/payments",
                            recordDto
                        );

                        if (response != null)
                        {
                            // Update local record
                            payment.IsSynced = true;
                            payment.LastSyncedAt = DateTime.UtcNow;
                            payment.ServerLastModified = DateTime.UtcNow;

                            result.ItemsSynced++;
                        }
                        else
                        {
                            result.ItemsFailed++;
                            result.Errors.Add($"Failed to sync payment for invoice {payment.InvoiceId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.ItemsFailed++;
                        result.Errors.Add($"Error syncing payment {payment.Id}: {ex.Message}");
                    }

                    processed++;
                    OnSyncProgress($"Syncing payments...", total, processed);
                }

                await _localDb.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Payment sync failed: {ex.Message}");
            }

            return result;
        }

        public async Task<bool> HasPendingChangesAsync()
        {
            var maintenanceCount = await _localDb.MaintenanceRequests.CountAsync(m => !m.IsSynced);
            var paymentCount = await _localDb.Payments.CountAsync(p => !p.IsSynced);

            return maintenanceCount > 0 || paymentCount > 0;
        }

        public async Task<Dictionary<string, int>> GetPendingCountsAsync()
        {
            return new Dictionary<string, int>
            {
                ["MaintenanceRequests"] = await _localDb.MaintenanceRequests.CountAsync(m => !m.IsSynced),
                ["Payments"] = await _localDb.Payments.CountAsync(p => !p.IsSynced)
            };
        }

        public async Task<DateTime?> GetLastSyncTimeAsync()
        {
            try
            {
                var lastSyncStr = await SecureStorage.GetAsync(LastSyncKey);
                if (string.IsNullOrEmpty(lastSyncStr))
                    return null;

                if (DateTime.TryParse(lastSyncStr, out var lastSync))
                    return lastSync;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task SaveLastSyncTimeAsync(DateTime time)
        {
            await SecureStorage.SetAsync(LastSyncKey, time.ToString("O"));
        }

        private async Task<string?> UploadFileAsync(string localPath, string category)
        {
            try
            {
                if (!await _fileService.FileExistsAsync(localPath))
                    return null;

                var fileStream = await _fileService.GetFileStreamAsync(localPath);
                if (fileStream == null)
                    return null;

                // TODO: Implement actual file upload to Firebase Storage or your file server
                // For now, we'll simulate a successful upload
                // In production, you would:
                // 1. Upload to Firebase Storage or your backend
                // 2. Get the public URL
                // 3. Return the URL

                // Placeholder: In real implementation, upload file and return URL
                var fileName = Path.GetFileName(localPath);
                var simulatedUrl = $"https://storage.example.com/{category}/{fileName}";

                // Mark file as uploaded in metadata
                // await _fileService.MarkFileAsUploadedAsync(localPath, simulatedUrl);

                return simulatedUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading file: {ex.Message}");
                return null;
            }
        }

        private void OnSyncProgress(string message, int total, int processed)
        {
            SyncProgressChanged?.Invoke(this, new SyncProgressEventArgs
            {
                Message = message,
                TotalItems = total,
                ProcessedItems = processed
            });
        }

        private void OnSyncCompleted(SyncResult result)
        {
            SyncCompleted?.Invoke(this, result);
        }
    }
}
