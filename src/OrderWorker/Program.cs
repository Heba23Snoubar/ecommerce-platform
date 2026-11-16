using OrderWorker;

var builder = Host.CreateApplicationBuilder(args);

// مسح أي مصادر إعدادات نصية والاعتماد حصراً على متغيرات البيئة
builder.Configuration.Sources.Clear();
builder.Configuration.AddEnvironmentVariables();

var rabbitSettings = RabbitMqSettings.FromEnvironment();
var smtpSettings = SmtpSettings.FromEnvironment();

builder.Services.AddSingleton(rabbitSettings);
builder.Services.AddSingleton(smtpSettings);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();