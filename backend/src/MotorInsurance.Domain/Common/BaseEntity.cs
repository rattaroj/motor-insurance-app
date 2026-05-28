namespace MotorInsurance.Domain.Common;

public abstract class BaseEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
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
