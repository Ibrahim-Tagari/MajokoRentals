using Google.Cloud.Firestore;

namespace MajokoRentals.Core.Services
{
    public interface IFirestoreService
    {
        Task<T> GetDocumentAsync<T>(string collection, string documentId) where T : class;
        Task<List<T>> GetCollectionAsync<T>(string collection) where T : class;
        Task<List<T>> QueryCollectionAsync<T>(string collection, string field, object value) where T : class;
        Task<string> AddDocumentAsync<T>(string collection, T data) where T : class;
        Task UpdateDocumentAsync<T>(string collection, string documentId, T data) where T : class;
        Task DeleteDocumentAsync(string collection, string documentId);
        Task<bool> DocumentExistsAsync(string collection, string documentId);
        Task<string?> FindDocumentIdByFieldAsync(string collection, string field, object value);

        // Advanced query methods for messaging/escalation features
        Task<List<T>> QueryCollectionWithMultipleFiltersAsync<T>(
            string collection,
            Dictionary<string, object> filters,
            string? orderByField = null,
            bool descending = false,
            int? limit = null) where T : class;

        Task<int> CountDocumentsAsync(string collection, Dictionary<string, object>? filters = null);

        // Subcollection support
        Task<string> AddSubcollectionDocumentAsync<T>(string parentCollection, string parentDocId, string subcollection, T data) where T : class;
        Task<List<T>> GetSubcollectionAsync<T>(string parentCollection, string parentDocId, string subcollection) where T : class;
        Task<List<T>> QuerySubcollectionAsync<T>(
            string parentCollection,
            string parentDocId,
            string subcollection,
            string? orderByField = null,
            bool descending = false,
            int? limit = null) where T : class;

        // Batch update support
        Task UpdateDocumentFieldsAsync(string collection, string documentId, Dictionary<string, object> fields);
    }
}
