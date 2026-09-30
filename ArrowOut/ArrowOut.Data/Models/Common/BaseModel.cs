namespace ArrowOut.Data.Models.Common;

// Every entity has an Id.
public abstract class BaseModel<TKey>
{
    public TKey Id { get; set; } = default!;
}

// Entities with created/modified dates. The DbContext fills them in.
public abstract class BaseAuditableModel<TKey> : BaseModel<TKey>, IAuditInfo
{
    public DateTime CreatedOn { get; set; }

    public DateTime? ModifiedOn { get; set; }
}
