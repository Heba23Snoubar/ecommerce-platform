
using UUIDNext;

namespace E.Domain.Common
{
    public abstract class BaseEntity
    {
        public Guid Id { get; protected set; } = Uuid.NewSequential();

        public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; protected set; }
    }

}
