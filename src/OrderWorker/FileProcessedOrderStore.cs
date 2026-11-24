using OrderWorker.Interface;
using System.Text.Json;

namespace OrderWorker;

public sealed class FileProcessedOrderStore : IProcessedOrderStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<Guid, ProcessedOrderRecord> _records;

    public FileProcessedOrderStore(string? storageDirectory = null)
    {
        var dir = string.IsNullOrWhiteSpace(storageDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : storageDirectory;

        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "processed-orders.json");
        _records = LoadFromDisk();
    }

    public async Task<bool> TryAcquireAsync(Guid orderId, Guid messageId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_records.ContainsKey(orderId))
                return false;

            _records[orderId] = new ProcessedOrderRecord(orderId, messageId, DateTime.UtcNow);
            await SaveToDiskAsync(cancellationToken);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_records.Remove(orderId))
            {
                await SaveToDiskAsync(cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> HasBeenProcessedAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _records.ContainsKey(orderId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task MarkAsProcessedAsync(Guid orderId, Guid messageId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_records.ContainsKey(orderId))
                return;

            _records[orderId] = new ProcessedOrderRecord(orderId, messageId, DateTime.UtcNow);
            await SaveToDiskAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private Dictionary<Guid, ProcessedOrderRecord> LoadFromDisk()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<Guid, ProcessedOrderRecord>();

        try
        {
            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<ProcessedOrderRecord>>(json);
            return list?.ToDictionary(x => x.OrderId) ?? new Dictionary<Guid, ProcessedOrderRecord>();
        }
        catch
        {
            return new Dictionary<Guid, ProcessedOrderRecord>();
        }
    }

    private async Task SaveToDiskAsync(CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(_records.Values, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }

    private sealed record ProcessedOrderRecord(Guid OrderId, Guid MessageId, DateTime ProcessedAtUtc);
}
