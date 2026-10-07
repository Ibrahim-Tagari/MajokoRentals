using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MajokoRentals.Core.Data;
using MajokoRentals.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure DbContext (needed by EscalationService and other services)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
// Cookie-based authentication (no more SQL Server Identity)
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

// SignalR for real-time notifications
builder.Services.AddSignalR();

// Add MVC and API support
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// CORS - secure configuration for production
builder.Services.AddCors(options =>
{
    options.AddPolicy("MobileApp", policy =>
    {
        var allowedOrigins = builder.Configuration["CORS:AllowedOrigins"]?.Split(',')
            ?? new[] { "http://localhost:3000", "http://localhost:5000" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add HttpClient factory BEFORE registering services that need it
builder.Services.AddHttpClient();

// Register Firebase services (Authentication + Firestore)
builder.Services.AddSingleton<IFirebaseAuthService, FirebaseAuthService>();
builder.Services.AddSingleton<IFirestoreService, FirestoreService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IFirebaseMvcAuthService, FirebaseMvcAuthService>();

// Register application services (MVC only needs these - no longer using InvoiceService/ChatbotService)
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();

// Register Firestore messaging and escalation services
builder.Services.AddScoped<IMessagingService, MessagingService>();
builder.Services.AddScoped<IEscalationService, EscalationService>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();

// Register API client services for MVC to call API
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IApiClientService, ApiClientService>();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Majoko Rentals API",
        Version = "v1",
        Description = "REST API for Majoko Rentals Property Management System",
        Contact = new OpenApiContact
        {
            Name = "Dr Majoko Projects",
            Email = "support@majoko.co.za"
        }
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
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

var app = builder.Build();

// Create default admin user in Firebase
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var firebaseAuth = services.GetRequiredService<IFirebaseMvcAuthService>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // Try to create default admin user
        try
        {
            logger.LogInformation("Checking for default admin user...");

            // Try to get admin by email
            var userProfile = await services.GetRequiredService<IUserProfileService>()
                .GetUserProfileByEmailAsync("admin@majoko.co.za");

            if (userProfile == null)
            {
                logger.LogInformation("Creating default admin user in Firebase...");

                await firebaseAuth.RegisterUserAsync(
                    "admin@majoko.co.za",
                    "Admin@123",
                    "System",
                    "Administrator",
                    "Admin"
                );

                logger.LogInformation("Default admin user created successfully!");
            }
            else
            {
                logger.LogInformation("Default admin user already exists.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Admin user may already exist or error occurred during creation.");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while setting up Firebase authentication.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
    // Enable Swagger in development
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Majoko Rentals API V1");
        c.RoutePrefix = "api/docs"; // Access Swagger UI at /api/docs
    });
}

app.UseHttpsRedirection();

// Add security headers
app.Use(async (context, next) =>
{
    // Prevent clickjacking
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

    // Prevent MIME type sniffing
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";

    // Enable XSS protection
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

    // Referrer policy
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // Content Security Policy (relaxed for MVC with inline scripts, Firebase, and Mapbox)
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://api.mapbox.com https://cdn.jsdelivr.net https://code.jquery.com https://www.gstatic.com; " +
        "style-src 'self' 'unsafe-inline' https://api.mapbox.com https://cdn.jsdelivr.net; " +
        "img-src 'self' data: https: blob:; " +
        "font-src 'self' data: https:; " +
        "connect-src 'self' https://api.mapbox.com https://events.mapbox.com https://identitytoolkit.googleapis.com https://securetoken.googleapis.com https://www.googleapis.com wss://localhost:* ws://localhost:*;";

    // Permissions policy
    context.Response.Headers["Permissions-Policy"] =
        "geolocation=(self), microphone=(), camera=()";

    // HSTS (HTTP Strict Transport Security) - only in production
    if (!context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseCors("MobileApp");

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// Map API endpoints
app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action}/{id?}");

app.Run();
