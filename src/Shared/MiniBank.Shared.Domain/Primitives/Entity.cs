namespace MiniBank.Shared.Domain.Primitives;

public abstract class Entity<TId> where TId : StronglyTypedId
{
    public TId Id { get; protected set; } = default!;
    public DateTime CreatedAt { get; protected set; }
    public DateTime UpdatedAt { get; protected set; }
    public int Version { get; protected set; }

    protected Entity() { }

    protected Entity(TId id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version = 1;
    }

    protected void MarkModified()
    {
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => Id.GetHashCode();
}
