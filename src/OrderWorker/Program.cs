using OrderWorker;
using OrderWorker.Implements;
using OrderWorker.Interface;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.Sources.Clear();
builder.Configuration.AddEnvironmentVariables();

var rabbitSettings = RabbitMqSettings.FromEnvironment();
var smtpSettings = SmtpSettings.FromEnvironment();
var redisSettings = RedisSettings.FromEnvironment();

builder.Services.AddSingleton(rabbitSettings);
builder.Services.AddSingleton(smtpSettings);
builder.Services.AddSingleton(redisSettings);

builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(
        redisSettings.ConnectionString));

builder.Services.AddSingleton<IProcessedOrderStore>(
    serviceProvider =>
    {
        var connection = serviceProvider
            .GetRequiredService<IConnectionMultiplexer>();

        var settings = serviceProvider
            .GetRequiredService<RedisSettings>();

        return new RedisProcessedOrderStore(
            connection,
            settings);
    });

builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();