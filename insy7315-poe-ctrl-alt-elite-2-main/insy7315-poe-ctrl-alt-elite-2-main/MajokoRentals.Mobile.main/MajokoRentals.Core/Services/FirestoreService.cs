using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace MajokoRentals.Core.Services
{
    public class FirestoreService : IFirestoreService
    {
        private FirestoreDb? _firestoreDb;
        private readonly ILogger<FirestoreService> _logger;
        private readonly string _projectId;
        private readonly bool _isMobile;
        private bool _initialized = false;
        private readonly object _initLock = new object();

        public FirestoreService(IConfiguration configuration, ILogger<FirestoreService> logger)
        {
            _logger = logger;
            _projectId = configuration["Firebase:ProjectId"] ?? throw new InvalidOperationException("Firebase ProjectId not configured");
            var serviceAccountKeyPath = configuration["Firebase:ServiceAccountKeyPath"];

            // Detect if this is a mobile environment (no service account key)
            _isMobile = string.IsNullOrEmpty(serviceAccountKeyPath);

            if (!_isMobile)
            {
                // Server environment - initialize immediately
                try
                {
                    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), serviceAccountKeyPath!);
                    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", fullPath);
                    _logger.LogInformation($"Using service account credentials from: {fullPath}");
                    _firestoreDb = FirestoreDb.Create(_projectId);
                    _initialized = true;
                    _logger.LogInformation("Firestore initialized successfully for server");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing Firestore");
                    throw;
                }
            }
            else
            {
                _logger.LogWarning("Mobile environment detected - Firestore operations will use REST API or fail. Consider using IUserProfileService from API instead.");
            }
        }

        private void EnsureInitialized()
        {
            if (_isMobile)
            {
                throw new NotSupportedException("Direct Firestore access is not supported in mobile apps. Use the API client instead.");
            }

            if (!_initialized)
            {
                lock (_initLock)
                {
                    if (!_initialized && _firestoreDb == null)
                    {
                        _firestoreDb = FirestoreDb.Create(_projectId);
                        _initialized = true;
                    }
                }
            }
        }

        public async Task<T> GetDocumentAsync<T>(string collection, string documentId) where T : class
        {
            EnsureInitialized();
            try
            {
                var docRef = _firestoreDb!.Collection(collection).Document(documentId);
                var snapshot = await docRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    _logger.LogWarning($"Document not found: {collection}/{documentId}");
                    return null!;
                }

                return snapshot.ConvertTo<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting document: {collection}/{documentId}");
                throw;
            }
        }

        public async Task<List<T>> GetCollectionAsync<T>(string collection) where T : class
        {
            EnsureInitialized();
            try
            {
                var collectionRef = _firestoreDb!.Collection(collection);
                var snapshot = await collectionRef.GetSnapshotAsync();

                var results = new List<T>();
                foreach (var document in snapshot.Documents)
                {
                    var data = document.ConvertTo<T>();

                    // If T is Dictionary<string, object>, include the document ID
                    if (data is Dictionary<string, object> dict && !dict.ContainsKey("uid"))
                    {
                        dict["uid"] = document.Id;
                    }
                    else
                    {
                        // Try to set Id property using reflection
                        var idProperty = typeof(T).GetProperty("Id");
                        if (idProperty != null && idProperty.CanWrite)
                        {
                            idProperty.SetValue(data, document.Id);
                        }
                    }

                    results.Add(data);
                }

                _logger.LogInformation($"Retrieved {results.Count} documents from collection: {collection}");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting collection: {collection}");
                throw;
            }
        }

        public async Task<List<T>> QueryCollectionAsync<T>(string collection, string field, object value) where T : class
        {
            EnsureInitialized();
            try
            {
                var collectionRef = _firestoreDb!.Collection(collection);
                var query = collectionRef.WhereEqualTo(field, value);
                var snapshot = await query.GetSnapshotAsync();

                var results = new List<T>();
                foreach (var document in snapshot.Documents)
                {
                    var data = document.ConvertTo<T>();

                    // If T is Dictionary<string, object>, include the document ID
                    if (data is Dictionary<string, object> dict && !dict.ContainsKey("uid"))
                    {
                        dict["uid"] = document.Id;
                    }
                    else
                    {
                        // Try to set Id property using reflection
                        var idProperty = typeof(T).GetProperty("Id");
                        if (idProperty != null && idProperty.CanWrite)
                        {
                            idProperty.SetValue(data, document.Id);
                        }
                    }

                    results.Add(data);
                }

                _logger.LogInformation($"Query returned {results.Count} documents from collection: {collection}");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error querying collection: {collection}");
                throw;
            }
        }

        public async Task<string> AddDocumentAsync<T>(string collection, T data) where T : class
        {
            EnsureInitialized();
            try
            {
                var collectionRef = _firestoreDb!.Collection(collection);
                var docRef = await collectionRef.AddAsync(data);

                _logger.LogInformation($"Document added to collection: {collection}, ID: {docRef.Id}");
                return docRef.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding document to collection: {collection}");
                throw;
            }
        }

        public async Task UpdateDocumentAsync<T>(string collection, string documentId, T data) where T : class
        {
            EnsureInitialized();
            try
            {
                var docRef = _firestoreDb!.Collection(collection).Document(documentId);
                await docRef.SetAsync(data, SetOptions.MergeAll);

                _logger.LogInformation($"Document updated: {collection}/{documentId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating document: {collection}/{documentId}");
                throw;
            }
        }

        public async Task DeleteDocumentAsync(string collection, string documentId)
        {
            EnsureInitialized();
            try
            {
                var docRef = _firestoreDb!.Collection(collection).Document(documentId);
                await docRef.DeleteAsync();

                _logger.LogInformation($"Document deleted: {collection}/{documentId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting document: {collection}/{documentId}");
                throw;
            }
        }

        public async Task<bool> DocumentExistsAsync(string collection, string documentId)
        {
            EnsureInitialized();
            try
            {
                var docRef = _firestoreDb!.Collection(collection).Document(documentId);
                var snapshot = await docRef.GetSnapshotAsync();
                return snapshot.Exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking document existence: {collection}/{documentId}");
                throw;
            }
        }

        public async Task<string?> FindDocumentIdByFieldAsync(string collection, string field, object value)
        {
            EnsureInitialized();
            try
            {
                var collectionRef = _firestoreDb!.Collection(collection);
                var query = collectionRef.WhereEqualTo(field, value);
                var snapshot = await query.GetSnapshotAsync();

                if (snapshot.Documents.Count == 0)
                {
                    _logger.LogWarning($"No document found in {collection} where {field} = {value}");
                    return null;
                }

                var documentId = snapshot.Documents[0].Id;
                _logger.LogInformation($"Found document ID: {documentId} in collection: {collection}");
                return documentId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding document ID in collection: {collection}");
                throw;
            }
        }

        public async Task<List<T>> QueryCollectionWithMultipleFiltersAsync<T>(
            string collection,
            Dictionary<string, object> filters,
            string? orderByField = null,
            bool descending = false,
            int? limit = null) where T : class
        {
            EnsureInitialized();
            try
            {
                Query query = _firestoreDb!.Collection(collection);

                // Apply filters
                foreach (var filter in filters)
                {
                    query = query.WhereEqualTo(filter.Key, filter.Value);
                }

                // Apply ordering
                if (!string.IsNullOrEmpty(orderByField))
                {
                    query = descending ? query.OrderByDescending(orderByField) : query.OrderBy(orderByField);
                }

                // Apply limit
                if (limit.HasValue)
                {
                    query = query.Limit(limit.Value);
                }

                var snapshot = await query.GetSnapshotAsync();

                var results = new List<T>();
                foreach (var document in snapshot.Documents)
                {
                    results.Add(document.ConvertTo<T>());
                }

                _logger.LogInformation($"Query returned {results.Count} documents from collection: {collection}");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error querying collection with filters: {collection}");
                throw;
            }
        }

        public async Task<int> CountDocumentsAsync(string collection, Dictionary<string, object>? filters = null)
        {
            EnsureInitialized();
            try
            {
                Query query = _firestoreDb!.Collection(collection);

                // Apply filters if provided
                if (filters != null)
                {
                    foreach (var filter in filters)
                    {
                        query = query.WhereEqualTo(filter.Key, filter.Value);
                    }
                }

                var snapshot = await query.GetSnapshotAsync();
                return snapshot.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error counting documents in collection: {collection}");
                throw;
            }
        }

        public async Task<string> AddSubcollectionDocumentAsync<T>(string parentCollection, string parentDocId, string subcollection, T data) where T : class
        {
            EnsureInitialized();
            try
            {
                var subcollectionRef = _firestoreDb!.Collection(parentCollection).Document(parentDocId).Collection(subcollection);
                var docRef = await subcollectionRef.AddAsync(data);

                _logger.LogInformation($"Document added to subcollection: {parentCollection}/{parentDocId}/{subcollection}, ID: {docRef.Id}");
                return docRef.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding document to subcollection: {parentCollection}/{parentDocId}/{subcollection}");
                throw;
            }
        }

        public async Task<List<T>> GetSubcollectionAsync<T>(string parentCollection, string parentDocId, string subcollection) where T : class
        {
            EnsureInitialized();
            try
            {
                var subcollectionRef = _firestoreDb!.Collection(parentCollection).Document(parentDocId).Collection(subcollection);
                var snapshot = await subcollectionRef.GetSnapshotAsync();

                var results = new List<T>();
                foreach (var document in snapshot.Documents)
                {
                    results.Add(document.ConvertTo<T>());
                }

                _logger.LogInformation($"Retrieved {results.Count} documents from subcollection: {parentCollection}/{parentDocId}/{subcollection}");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting subcollection: {parentCollection}/{parentDocId}/{subcollection}");
                throw;
            }
        }

        public async Task<List<T>> QuerySubcollectionAsync<T>(
            string parentCollection,
            string parentDocId,
            string subcollection,
            string? orderByField = null,
            bool descending = false,
            int? limit = null) where T : class
        {
            EnsureInitialized();
            try
            {
                Query query = _firestoreDb!.Collection(parentCollection).Document(parentDocId).Collection(subcollection);

                // Apply ordering
                if (!string.IsNullOrEmpty(orderByField))
                {
                    query = descending ? query.OrderByDescending(orderByField) : query.OrderBy(orderByField);
                }

                // Apply limit
                if (limit.HasValue)
                {
                    query = query.Limit(limit.Value);
                }

                var snapshot = await query.GetSnapshotAsync();

                var results = new List<T>();
                foreach (var document in snapshot.Documents)
                {
                    results.Add(document.ConvertTo<T>());
                }

                _logger.LogInformation($"Query returned {results.Count} documents from subcollection: {parentCollection}/{parentDocId}/{subcollection}");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error querying subcollection: {parentCollection}/{parentDocId}/{subcollection}");
                throw;
            }
        }

        public async Task UpdateDocumentFieldsAsync(string collection, string documentId, Dictionary<string, object> fields)
        {
            EnsureInitialized();
            try
            {
                var docRef = _firestoreDb!.Collection(collection).Document(documentId);
                await docRef.UpdateAsync(fields);

                _logger.LogInformation($"Document fields updated: {collection}/{documentId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating document fields: {collection}/{documentId}");
                throw;
            }
        }
    }
}
