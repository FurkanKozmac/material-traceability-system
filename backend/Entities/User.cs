namespace backend.Entities;

public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AuthSource { get; set; } = "DB";
    public bool Active { get; set; } = true;
    public ICollection<Role> Roles { get; set; } = [];
}
