namespace ToDos.Web.Components.Auth;

public record LoginRequest(string Username, string Password, bool RememberMe = false);
public record LoginResult(string Username, string[] Roles);