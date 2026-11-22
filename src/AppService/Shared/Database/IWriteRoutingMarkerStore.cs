namespace Shared.Database
{
    public interface IWriteRoutingMarkerStore
    {
        Task<bool> HasRecentWriteAsync(
            CancellationToken cancellationToken = default);

        Task MarkWriteAsync(
            CancellationToken cancellationToken = default);
    }
}