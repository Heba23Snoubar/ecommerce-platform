using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Catalog.Module.Endpoints.Contoller;
using Catalog.Module.Infrastructure;
using Infrastructure.Warehouse;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Notifications.Module;
using Orders.Module;
using Shared.Errors;
using Users.Module;
using Carts.Module.Infrastructure;


var builder = WebApplication.CreateBuilder(args);

// Register Modules & Infrastructure
builder.Services.AddUsersModule(builder.Configuration);
builder.Services.AddOrdersModule(builder.Configuration);
builder.Services.AddCatalogInfrastructure(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddWarehouseInfrastructure(builder.Configuration);
builder.Services.AddCartInfrastructure(builder.Configuration);

// JWT Configuration
var jwtKey = builder.Configuration["JWT_KEY"]
    ?? throw new InvalidOperationException(
        "JWT_KEY configuration is missing.");

var jwtIssuer = builder.Configuration["JWT_ISSUER"]
    ?? throw new InvalidOperationException(
        "JWT_ISSUER configuration is missing.");

var jwtAudience = builder.Configuration["JWT_AUDIENCE"]
    ?? throw new InvalidOperationException(
        "JWT_AUDIENCE configuration is missing.");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "JWT_KEY must contain at least 32 bytes.");
}

// Authentication
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)),

                ValidateLifetime = true,
                RequireExpirationTime = true,

                ClockSkew = TimeSpan.FromSeconds(30),

                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role,

                ValidAlgorithms =
                [
                    SecurityAlgorithms.HmacSha256
                ]
            };
    });

// Authorization
builder.Services.AddAuthorization();

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }
        )
    );

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";

        await context.HttpContext.Response.WriteAsJsonAsync(
            new ApiError(
                "RATE_LIMIT_EXCEEDED",
                "Too many login attempts. Please try again later."
            ),
            cancellationToken
        );
    };
});

// Controllers + Application Parts + Validation + Enums
builder.Services.AddControllers()
    .AddApplicationPart(typeof(UsersModule).Assembly)
    .AddApplicationPart(OrdersModule.ModuleAssembly)
    .AddApplicationPart(typeof(ProductsController).Assembly)
    .AddApplicationPart(NotificationsModuleExtensions.ModuleAssembly)
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var details = context.ModelState
                .Where(entry => entry.Value!.Errors.Count > 0)
                .SelectMany(entry => entry.Value!.Errors.Select(error =>
                    $"{entry.Key}: {error.ErrorMessage}"));

            var error = new ApiError(
                "VALIDATION_ERROR",
                "البيانات المدخلة غير صحيحة",
                details
            );

            return new ObjectResult(error)
            {
                StatusCode = 422
            };
        };
    });

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddMvc();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply Database Migrations
app.Services.MigrateUsersModule();
app.Services.MigrateOrdersModule();
app.Services.MigrateNotificationsModule();

// HSTS - Enable only outside Development
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Security Headers Middleware
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";

    headers["Permissions-Policy"] =
        "camera=(), microphone=(), geolocation=()";

    if (!context.Request.Path.StartsWithSegments(
        "/swagger",
        StringComparison.OrdinalIgnoreCase))
    {
        headers["Content-Security-Policy"] =
            "default-src 'none'; frame-ancestors 'none'; " +
            "base-uri 'none'; form-action 'none'";
    }

    await next();
});

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Error Handling Middleware
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseRouting();

app.UseRateLimiter();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();