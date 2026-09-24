namespace backend.Entities;

public class Address
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int MaxCapacity { get; set; }
    public string StorageType { get; set; } = "GENERAL";
    public ICollection<Unit> Units { get; set; } = [];
}
