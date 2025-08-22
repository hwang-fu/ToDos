namespace ToDos.Web.Components.Auth;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

public interface IUserStore
{
    bool Validate(string username, string password, out UserRecord? user);
}

public record UserRecord(string Username, string Password, string[] Roles);

public class InMemoryUserStore : IUserStore
{
    // ⚠️ Demo only. Don’t store plain passwords in real apps.
    private static readonly List<UserRecord> Users = new()
    {
        new("admin", "admin123", new[] { "Admin" }),
        new("alice", "alice123", new[] { "User" })
    };

    public bool Validate(string username, string password, out UserRecord? user)
    {
        user = Users.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)
            && u.Password == password);
        return user is not null;
    }
}

public static class AuthHelpers
{
    public static ClaimsPrincipal ToPrincipal(UserRecord user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(
            claims, 
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        
        return new ClaimsPrincipal(identity);
    }
}