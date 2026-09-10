namespace MiniPayroll.Domain.Entities;

public class PlatformCity
{
    public Guid Id { get; set; }
    public Guid StateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public PlatformState State { get; set; } = null!;
}
