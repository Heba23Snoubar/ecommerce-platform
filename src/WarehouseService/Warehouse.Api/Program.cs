using Microsoft.EntityFrameworkCore;
using Warehouse.Api.Data;
using Warehouse.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// تسجيل قاعدة بيانات المستودع
builder.Services.AddDbContext<WarehouseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WarehouseDb")));

// إضافة خدمات gRPC
builder.Services.AddGrpc();

var app = builder.Build();

// إنشاء الجداول تلقائياً إن لم تكن موجودة
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
    db.Database.EnsureCreated();
}

// توجيه خدمة gRPC
app.MapGrpcService<WarehouseGrpcService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();
