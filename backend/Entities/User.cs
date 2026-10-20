namespace backend.Entities;

public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AuthSource { get; set; } = "DB";
    public bool Active { get; set; } = true;
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public ICollection<Role> Roles { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
