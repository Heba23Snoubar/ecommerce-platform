using OrderWorker;
using OrderWorker.Implements;
using OrderWorker.Interface;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.Sources.Clear();
builder.Configuration.AddEnvironmentVariables();

var rabbitSettings = RabbitMqSettings.FromEnvironment();
var smtpSettings = SmtpSettings.FromEnvironment();
var redisSettings = RedisSettings.FromEnvironment();

builder.Services.AddSingleton(rabbitSettings);
builder.Services.AddSingleton(smtpSettings);
builder.Services.AddSingleton(redisSettings);
builder.Services.AddSingleton<IProcessedOrderStore, RedisProcessedOrderStore>();
var useFakeEmail = string.Equals(
    Environment.GetEnvironmentVariable("EMAIL_SENDER"), "fake", StringComparison.OrdinalIgnoreCase);

if (useFakeEmail)
    builder.Services.AddSingleton<IEmailSender, FakeEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
