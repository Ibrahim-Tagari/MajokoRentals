using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajokoRentals.Core.DTOs;
using MajokoRentals.Core.Models;
using MajokoRentals.Core.Services;
using System.Text.Json;

namespace MajokoRentals.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PropertiesController : ControllerBase
    {
        private readonly IFirestoreService _firestore;
        private readonly ILogger<PropertiesController> _logger;

        public PropertiesController(
            IFirestoreService firestore,
            ILogger<PropertiesController> logger)
        {
            _firestore = firestore;
            _logger = logger;
        }

        /// <summary>
        /// Get all properties with optional filtering
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<PropertyListDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProperties(
            [FromQuery] PropertyStatus? status = null,
            [FromQuery] int? minBedrooms = null,
            [FromQuery] decimal? maxRent = null)
        {
            try
            {
                // Get all properties from Firestore
                var allProperties = await _firestore.GetCollectionAsync<Dictionary<string, object>>("properties");

                // Filter in memory
                var filteredProperties = allProperties
                    .Where(p =>
                    {
                        bool matches = true;

                        // Status filter
                        if (status.HasValue && p.ContainsKey("status"))
                        {
                            var propStatus = Enum.Parse<PropertyStatus>(p["status"].ToString()!);
                            matches = matches && propStatus == status.Value;
                        }

                        // Bedrooms filter
                        if (minBedrooms.HasValue && p.ContainsKey("bedrooms"))
                        {
                            var bedrooms = Convert.ToInt32(p["bedrooms"]);
                            matches = matches && bedrooms >= minBedrooms.Value;
                        }

                        // Rent filter
                        if (maxRent.HasValue && p.ContainsKey("monthlyRent"))
                        {
                            var rent = Convert.ToDecimal(p["monthlyRent"]);
                            matches = matches && rent <= maxRent.Value;
                        }

                        return matches;
                    })
                    .OrderBy(p => p.ContainsKey("name") ? p["name"].ToString() : "")
                    .Select(p => new PropertyListDto
                    {
                        Id = p.ContainsKey("id") ? Convert.ToInt64(p["id"]) : 0,
                        Name = p.ContainsKey("name") ? p["name"].ToString()! : "",
                        Address = p.ContainsKey("address") ? p["address"].ToString()! : "",
                        City = p.ContainsKey("city") ? p["city"].ToString()! : "",
                        MonthlyRent = p.ContainsKey("monthlyRent") ? Convert.ToDouble(p["monthlyRent"]) : 0,
                        Bedrooms = p.ContainsKey("bedrooms") ? Convert.ToInt32(p["bedrooms"]) : 0,
                        Bathrooms = p.ContainsKey("bathrooms") ? Convert.ToDouble(p["bathrooms"]) : 0,
                        Status = p.ContainsKey("status") ? Enum.Parse<PropertyStatus>(p["status"].ToString()!) : PropertyStatus.Available,
                        MainImage = p.ContainsKey("images") && p["images"] is List<object> images && images.Count > 0
                            ? images[0].ToString()
                            : null
                    })
                    .ToList();

                return Ok(filteredProperties);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching properties from Firestore");
                return StatusCode(500, new { message = "Error fetching properties" });
            }
        }

        /// <summary>
        /// Get a specific property by ID (accepts either Firestore document ID or numeric property ID)
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PropertyDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProperty(string id)
        {
            try
            {
                Dictionary<string, object>? property = null;

                // Try to parse as numeric ID first
                if (long.TryParse(id, out long numericId))
                {
                    // Search for property by numeric ID
                    var allProperties = await _firestore.GetCollectionAsync<Dictionary<string, object>>("properties");
                    property = allProperties.FirstOrDefault(p =>
                        p.ContainsKey("id") && Convert.ToInt64(p["id"]) == numericId);
                }
                else
                {
                    // Try as Firestore document ID
                    property = await _firestore.GetDocumentAsync<Dictionary<string, object>>("properties", id);
                }

                if (property == null)
                    return NotFound();

                var propertyDto = new PropertyDto
                {
                    Id = property.ContainsKey("id") ? Convert.ToInt64(property["id"]) : 0,
                    Name = property.ContainsKey("name") ? property["name"].ToString()! : "",
                    Description = property.ContainsKey("description") ? property["description"].ToString() : "",
                    Address = property.ContainsKey("address") ? property["address"].ToString()! : "",
                    City = property.ContainsKey("city") ? property["city"].ToString()! : "",
                    Province = property.ContainsKey("province") ? property["province"].ToString() : "",
                    PostalCode = property.ContainsKey("postalCode") ? property["postalCode"].ToString() : "",
                    Latitude = property.ContainsKey("latitude") ? Convert.ToDouble(property["latitude"]) : (double?)null,
                    Longitude = property.ContainsKey("longitude") ? Convert.ToDouble(property["longitude"]) : (double?)null,
                    MonthlyRent = property.ContainsKey("monthlyRent") ? Convert.ToDouble(property["monthlyRent"]) : 0,
                    DepositAmount = property.ContainsKey("depositAmount") ? Convert.ToDouble(property["depositAmount"]) : 0,
                    Bedrooms = property.ContainsKey("bedrooms") ? Convert.ToInt32(property["bedrooms"]) : 0,
                    Bathrooms = property.ContainsKey("bathrooms") ? Convert.ToDouble(property["bathrooms"]) : 0,
                    ParkingSpaces = property.ContainsKey("parkingSpaces") ? Convert.ToInt32(property["parkingSpaces"]) : (int?)null,
                    SquareMeters = property.ContainsKey("squareMeters") ? Convert.ToDouble(property["squareMeters"]) : (double?)null,
                    Status = property.ContainsKey("status") ? Enum.Parse<PropertyStatus>(property["status"].ToString()!) : PropertyStatus.Available,
                    IsFurnished = property.ContainsKey("isFurnished") && Convert.ToBoolean(property["isFurnished"]),
                    PetsAllowed = property.ContainsKey("petsAllowed") && Convert.ToBoolean(property["petsAllowed"]),
                    Amenities = property.ContainsKey("amenities") && property["amenities"] is List<object> amenities
                        ? amenities.Select(a => a.ToString()!).ToList()
                        : new List<string>(),
                    Images = property.ContainsKey("images") && property["images"] is List<object> images
                        ? images.Select(i => i.ToString()!).ToList()
                        : new List<string>(),
                    VideoUrl = property.ContainsKey("videoUrl") ? property["videoUrl"].ToString() : null,
                    CreatedAt = property.ContainsKey("createdAt") ? DateTime.Parse(property["createdAt"].ToString()!) : DateTime.UtcNow
                };

                return Ok(propertyDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching property {id} from Firestore");
                return StatusCode(500, new { message = "Error fetching property" });
            }
        }

        /// <summary>
        /// Create a new property (Admin/PropertyManager only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,PropertyManager")]
        [ProducesResponseType(typeof(PropertyDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateProperty([FromBody] CreatePropertyDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // Generate a numeric ID based on timestamp
                var id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var propertyData = new Dictionary<string, object>
                {
                    { "id", id },
                    { "name", dto.Name },
                    { "description", dto.Description ?? "" },
                    { "address", dto.Address },
                    { "city", dto.City },
                    { "province", dto.Province ?? "" },
                    { "postalCode", dto.PostalCode ?? "" },
                    { "monthlyRent", dto.MonthlyRent },
                    { "depositAmount", dto.DepositAmount },
                    { "bedrooms", dto.Bedrooms },
                    { "bathrooms", dto.Bathrooms },
                    { "parkingSpaces", dto.ParkingSpaces ?? 0 },
                    { "squareMeters", dto.SquareMeters ?? 0 },
                    { "status", dto.Status.ToString() },
                    { "isFurnished", dto.IsFurnished },
                    { "petsAllowed", dto.PetsAllowed },
                    { "amenities", dto.Amenities ?? new List<string>() },
                    { "images", dto.Images ?? new List<string>() },
                    { "videoUrl", dto.VideoUrl ?? "" },
                    { "createdAt", DateTime.UtcNow.ToString("o") },
                    { "updatedAt", DateTime.UtcNow.ToString("o") }
                };

                if (dto.Latitude.HasValue) propertyData["latitude"] = dto.Latitude.Value;
                if (dto.Longitude.HasValue) propertyData["longitude"] = dto.Longitude.Value;

                // Save to Firestore
                var docId = await _firestore.AddDocumentAsync("properties", propertyData);

                _logger.LogInformation($"Property created in Firestore with ID: {docId}");

                return CreatedAtAction(nameof(GetProperty), new { id = docId }, new { id = docId, propertyId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating property in Firestore");
                return StatusCode(500, new { message = "Error creating property" });
            }
        }

        /// <summary>
        /// Update an existing property (Admin/PropertyManager only) - accepts either Firestore document ID or numeric property ID
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,PropertyManager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateProperty(string id, [FromBody] UpdatePropertyDto dto)
        {
            try
            {
                string? documentId = null;

                // Try to parse as numeric ID first
                if (long.TryParse(id, out long numericId))
                {
                    // Search for property by numeric ID to get the Firestore document ID
                    documentId = await _firestore.FindDocumentIdByFieldAsync("properties", "id", numericId);
                }
                else
                {
                    // Use as Firestore document ID
                    documentId = id;
                }

                if (documentId == null)
                    return NotFound();

                // Check if property exists
                var existingProperty = await _firestore.GetDocumentAsync<Dictionary<string, object>>("properties", documentId);
                if (existingProperty == null)
                    return NotFound();

                var propertyData = new Dictionary<string, object>
                {
                    { "id", dto.Id },
                    { "name", dto.Name },
                    { "description", dto.Description ?? "" },
                    { "address", dto.Address },
                    { "city", dto.City },
                    { "province", dto.Province ?? "" },
                    { "postalCode", dto.PostalCode ?? "" },
                    { "monthlyRent", dto.MonthlyRent },
                    { "depositAmount", dto.DepositAmount },
                    { "bedrooms", dto.Bedrooms },
                    { "bathrooms", dto.Bathrooms },
                    { "parkingSpaces", dto.ParkingSpaces ?? 0 },
                    { "squareMeters", dto.SquareMeters ?? 0 },
                    { "status", dto.Status.ToString() },
                    { "isFurnished", dto.IsFurnished },
                    { "petsAllowed", dto.PetsAllowed },
                    { "amenities", dto.Amenities ?? new List<string>() },
                    { "videoUrl", dto.VideoUrl ?? "" },
                    { "updatedAt", DateTime.UtcNow.ToString("o") }
                };

                if (dto.Latitude.HasValue) propertyData["latitude"] = dto.Latitude.Value;
                if (dto.Longitude.HasValue) propertyData["longitude"] = dto.Longitude.Value;

                // Preserve images if they exist
                if (existingProperty.ContainsKey("images"))
                {
                    propertyData["images"] = existingProperty["images"];
                }

                // Preserve creation date
                if (existingProperty.ContainsKey("createdAt"))
                {
                    propertyData["createdAt"] = existingProperty["createdAt"];
                }

                await _firestore.UpdateDocumentAsync("properties", documentId, propertyData);

                _logger.LogInformation($"Property {id} (document ID: {documentId}) updated in Firestore");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating property {id} in Firestore");
                return StatusCode(500, new { message = "Error updating property" });
            }
        }

        /// <summary>
        /// Delete a property (Admin only) - accepts either Firestore document ID or numeric property ID
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteProperty(string id)
        {
            try
            {
                string? documentId = null;

                // Try to parse as numeric ID first
                if (long.TryParse(id, out long numericId))
                {
                    // Search for property by numeric ID to get the Firestore document ID
                    documentId = await _firestore.FindDocumentIdByFieldAsync("properties", "id", numericId);
                }
                else
                {
                    // Use as Firestore document ID
                    documentId = id;
                }

                if (documentId == null)
                    return NotFound();

                // Check if property exists
                var existingProperty = await _firestore.GetDocumentAsync<Dictionary<string, object>>("properties", documentId);
                if (existingProperty == null)
                    return NotFound();

                await _firestore.DeleteDocumentAsync("properties", documentId);

                _logger.LogInformation($"Property {id} (document ID: {documentId}) deleted from Firestore");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting property {id} from Firestore");
                return StatusCode(500, new { message = "Error deleting property" });
            }
        }
    }
}
