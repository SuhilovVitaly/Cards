using System.Globalization;
using System.Reflection;

namespace Cards.Web.Services;

public class AppInfoService : IAppInfoService
{
    public AppInfoService()
    {
        var assembly = typeof(AppInfoService).Assembly;

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

        // InformationalVersion may contain "+commit" suffix when SourceRevisionId is set
        var plusIndex = informationalVersion.IndexOf('+');
        if (plusIndex >= 0)
        {
            Version = informationalVersion[..plusIndex];
            GitCommit = informationalVersion[(plusIndex + 1)..];
        }
        else
        {
            Version = informationalVersion;
            GitCommit = null;
        }

        var buildDateRaw = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, "BuildDateUtc", StringComparison.Ordinal))?
            .Value;

        BuildDateUtc = DateTime.TryParse(
            buildDateRaw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    public string Version { get; }

    public string? GitCommit { get; }

    public DateTime? BuildDateUtc { get; }
}
