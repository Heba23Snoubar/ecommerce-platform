using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Shared.Errors;
using Catalog.Module.Infrastructure;
using Catalog.Module.Endpoints.Contoller;
using Contracts.Warehouse;

var builder = WebApplication.CreateBuilder(args);



var warehouseUrl = builder.Configuration["Warehouse:Url"]
    ?? throw new InvalidOperationException("Configuration 'Warehouse:Url' is missing.");

builder.Services.AddGrpcClient<WarehouseService.WarehouseServiceClient>(options =>
{
    options.Address = new Uri(warehouseUrl);
});


builder.Services.AddControllers()
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

app.UseAuthorization();

app.MapControllers();

app.Run();
