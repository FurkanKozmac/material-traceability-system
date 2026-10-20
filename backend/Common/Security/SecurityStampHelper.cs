using backend.Entities;

namespace backend.Common.Security;

public static class SecurityStampHelper
{
    public static void Rotate(User user) => user.SecurityStamp = Guid.NewGuid().ToString("N");
}
