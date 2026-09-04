namespace AsistenteArtemis.Client.Maui.Services;

public static class ApiConfig
{
    // The Android emulator can't reach the host machine via "localhost" — it needs the
    // special alias 10.0.2.2. Windows runs in the same machine as the API, so localhost works.
    public static string BaseUrl =>
#if ANDROID
        "http://10.0.2.2:5039/api/";
#else
        "http://localhost:5039/api/";
#endif

    // Same host as BaseUrl, without the "/api/" suffix — SignalR hubs are mounted at the
    // server root (/hubs/...), not under /api. Same trick as hubBaseUrl() in
    // TodoAppRoll.Web/src/hooks/useMonitorAlerts.ts.
    public static string HubBaseUrl => BaseUrl.TrimEnd('/')[..^4];
}
