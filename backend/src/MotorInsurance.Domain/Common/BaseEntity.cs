namespace MotorInsurance.Domain.Common;

public abstract class BaseEntity
{
    public long Id { get; set; }

    // Audit columns. CreatedUser/CreatedAt are stamped on insert and UpdatedUser/UpdatedAt
    // on every update by AppDbContext.SaveChangesAsync (see Infrastructure). User fields hold
    // the username from ICurrentUser; they are null for non-request work (e.g. data seeding).
    public string? CreatedUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UpdatedUser { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public abstract class AuditableEntity : BaseEntity
{
    public byte[] RowVersion { get; set; } = default!;
}

/// <summary>Thrown when a state-machine transition rule is violated.</summary>
public class InvalidStateTransitionException : Exception
{
    public InvalidStateTransitionException(string entity, string from, string to)
        : base($"Illegal {entity} transition: {from} -> {to}") { }
}
