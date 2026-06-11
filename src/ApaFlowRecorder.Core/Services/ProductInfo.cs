using System.Reflection;

namespace ApaFlowRecorder.Core.Services;

public static class ProductInfo
{
    public const string Name = "Workflow Spec Recorder";
    public const string Author = "Yumin";

    public static string Version => ReadInformationalVersion();
    public static string DisplayVersion => $"版本 {Version}";
    public static string DisplayAttribution => $"作者 {Author}";
    public static string DisplaySummary => $"{DisplayVersion} · {DisplayAttribution}";

    private static string ReadInformationalVersion()
    {
        var version = typeof(ProductInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            version = typeof(ProductInfo).Assembly.GetName().Version?.ToString();
        }

        return string.IsNullOrWhiteSpace(version)
            ? "0.0.0"
            : version.Split('+')[0];
    }
}
