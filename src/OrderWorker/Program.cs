using OrderWorker;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.Sources.Clear();
builder.Configuration.AddEnvironmentVariables();

var rabbitSettings = RabbitMqSettings.FromEnvironment();
var smtpSettings = SmtpSettings.FromEnvironment();

var idempotencyDir = Environment.GetEnvironmentVariable("IDEMPOTENCY_DATA_DIR") ?? "/app/data";

builder.Services.AddSingleton(rabbitSettings);
builder.Services.AddSingleton(smtpSettings);
builder.Services.AddSingleton<IProcessedOrderStore>(_ => new FileProcessedOrderStore(idempotencyDir));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();