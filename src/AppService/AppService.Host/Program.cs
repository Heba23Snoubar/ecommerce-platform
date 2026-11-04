using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Catalog.Module.Endpoints.Contoller;
using Catalog.Module.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Shared.Errors;
using Users.Module;

var builder = WebApplication.CreateBuilder(args);

// Register Users Module
builder.Services.AddUsersModule(builder.Configuration);

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

// Controllers + Unified Validation Errors
builder.Services.AddControllers()
    .AddApplicationPart(typeof(UsersModule).Assembly)
    .AddApplicationPart(typeof(ProductsController).Assembly)
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
}).AddMvc();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCatalogInfrastructure(builder.Configuration);
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger
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

// Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
