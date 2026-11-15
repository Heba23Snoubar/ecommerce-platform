namespace OrderWorker;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly RabbitMqSettings _rabbitSettings;
    private readonly SmtpSettings _smtpSettings;

    public Worker(
        ILogger<Worker> logger,
        RabbitMqSettings rabbitSettings,
        SmtpSettings smtpSettings)
    {
        _logger = logger;
        _rabbitSettings = rabbitSettings;
        _smtpSettings = smtpSettings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OrderWorker started. RabbitMQ: {Host}:{Port} (User: {User}) | SMTP: {SmtpHost}:{SmtpPort} (From: {From})",
            _rabbitSettings.Host,
            _rabbitSettings.Port,
            _rabbitSettings.User,
            string.IsNullOrWhiteSpace(_smtpSettings.Host) ? "<not-set>" : _smtpSettings.Host,
            _smtpSettings.Port,
            string.IsNullOrWhiteSpace(_smtpSettings.From) ? "<not-set>" : _smtpSettings.From);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}