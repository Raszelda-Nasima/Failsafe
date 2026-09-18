namespace Failsafe.Web.Services;

// A simple scoped holder for the current circuit's access token. Scoped
// lifetime means one instance per Blazor Server circuit (effectively per
// connected user), set once when the circuit starts and read from
// anywhere in that same circuit afterward — this sidesteps the
// unreliable HttpContext-inside-a-component-lifecycle-method problem
// entirely, since the token is captured once, early, and cached.
public class CurrentUserTokenProvider
{
    public string? AccessToken { get; set; }
}