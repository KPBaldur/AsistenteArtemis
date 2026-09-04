namespace AsistenteArtemis.Client.Maui.Services;

// Tiny observable session holder so the layout/nav can react to login/logout without a
// full state-management library — the app is small enough that this is enough.
public class AuthState
{
    public bool IsAuthenticated { get; private set; }
    public string? Username { get; private set; }

    public event Action? Changed;

    public void SetAuthenticated(string username)
    {
        IsAuthenticated = true;
        Username = username;
        Changed?.Invoke();
    }

    public void SetLoggedOut()
    {
        IsAuthenticated = false;
        Username = null;
        Changed?.Invoke();
    }
}
