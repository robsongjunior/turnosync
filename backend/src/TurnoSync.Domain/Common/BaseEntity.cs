namespace TurnoSync.Domain.Common;

public abstract class BaseEntity
{
    public int Id { get; private set; }

    public bool IsDeleted { get; private set; }

    public void Delete() => IsDeleted = true;
}
