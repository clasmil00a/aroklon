namespace Aroklon.App.Services;

public static class AppInfo
{
    public static string Version =>
        typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
}
