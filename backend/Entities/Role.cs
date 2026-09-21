namespace backend.Entities;

public class Role
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<User> Users { get; set; } = [];
}
