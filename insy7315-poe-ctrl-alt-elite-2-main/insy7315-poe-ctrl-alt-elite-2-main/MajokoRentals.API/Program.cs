using Microsoft.OpenApi.Models;
using MajokoRentals.API.Authentication;
using MajokoRentals.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Simple port configuration - works everywhere
// Azure uses PORT environment variable, local uses 5275
var port = Environment.GetEnvironmentVariable("PORT") ?? "5275";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.
builder.Services.AddControllers();

// Firebase Authentication
builder.Services.AddAuthentication("Firebase")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, FirebaseAuthenticationHandler>("Firebase", null);

builder.Services.AddAuthorization();

// Add HttpClient factory BEFORE registering services that need it
builder.Services.AddHttpClient();

// Register Firebase services (primary data layer)
builder.Services.AddSingleton<IFirebaseAuthService, FirebaseAuthService>();
builder.Services.AddSingleton<IFirestoreService, FirestoreService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IFirebaseMvcAuthService, FirebaseMvcAuthService>();

// Register application services (API only needs these)
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
// Note: InvoiceService and ChatbotService removed - API controllers work directly with Firestore

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Majoko Rentals API",
        Version = "v1",
        Description = "REST API for Majoko Rentals Property Management System - Firebase Authentication",
        Contact = new OpenApiContact
        {
            Name = "Dr Majoko Projects",
            Email = "info@majokoprojects.com"
        }
    });

    // Configure Firebase Bearer authentication in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Firebase ID Token Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your Firebase ID token. Example: 'Bearer eyJhbGciOiJSUzI1NiIsImtpZCI6Ij...'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS configuration - secure for production
builder.Services.AddCors(options =>
{
    options.AddPolicy("SecurePolicy", policy =>
    {
        var allowedOrigins = builder.Configuration["CORS:AllowedOrigins"]?.Split(',')
            ?? new[] { "http://localhost:3000", "http://localhost:5000", "http://localhost:5275", "https://localhost:7158" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Seed initial admin user in Firebase
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var firebaseMvcAuth = services.GetRequiredService<IFirebaseMvcAuthService>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // Create default admin user if doesn't exist
        try
        {
            var adminProfile = await firebaseMvcAuth.GetUserProfileAsync("admin-uid");
            if (adminProfile == null)
            {
                logger.LogInformation("Creating default admin user...");
                await firebaseMvcAuth.RegisterUserAsync(
                    "admin@majoko.co.za",
                    "Admin@123",
                    "System",
                    "Administrator",
                    "Admin"
                );
                logger.LogInformation("Default admin user created successfully");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Admin user may already exist or error occurred: {Message}", ex.Message);
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the admin user.");
    }
}

// Configure the HTTP request pipeline.
// Enable Swagger in all environments (including production)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Majoko Rentals API V1");
    c.RoutePrefix = string.Empty; // Swagger at root URL
});

if (!app.Environment.IsDevelopment())
{
    // Only redirect to HTTPS in production
    app.UseHttpsRedirection();
}

// Add security headers
app.Use(async (context, next) =>
{
    // Prevent clickjacking
    context.Response.Headers["X-Frame-Options"] = "DENY";

    // Prevent MIME type sniffing
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";

    // Enable XSS protection
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

    // Referrer policy
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // Content Security Policy
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:;";

    // Permissions policy
    context.Response.Headers["Permissions-Policy"] =
        "geolocation=(), microphone=(), camera=()";

    // HSTS (HTTP Strict Transport Security) - only in production
    if (!context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    await next();
});

app.UseCors("SecurePolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
