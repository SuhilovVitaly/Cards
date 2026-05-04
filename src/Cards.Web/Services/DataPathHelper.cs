namespace Cards.Web.Services;

/// <summary>
/// Resolves the on-disk location for JSON data folders so that user data survives
/// redeploys to Azure App Service.
/// </summary>
/// <remarks>
/// On Azure App Service the deployment process wipes the contents of
/// <c>D:\home\site\wwwroot\</c> on every push, which means anything stored under
/// <c>ContentRootPath/Data</c> is lost. The platform exposes a separate persistent
/// area at <c>%HOME%\data\</c> that is preserved across deploys; we use it whenever
/// the application is running inside App Service and fall back to the project content
/// root for local development.
/// </remarks>
public static class DataPathHelper
{
    private const string AzureDataSubfolder = "Cards";
    private const string LegacyRootFolder = "Data";

    /// <summary>
    /// Returns a writable folder for the given entity (for example "users"), creating
    /// it if missing and migrating any files from the legacy location on first run.
    /// </summary>
    public static string PrepareEntityPath(IWebHostEnvironment environment, string entity)
    {
        var newPath = Path.Combine(GetDataRoot(environment), entity);
        Directory.CreateDirectory(newPath);

        // One-time migration from the old ContentRoot-based location
        var legacyPath = Path.Combine(environment.ContentRootPath, LegacyRootFolder, entity);
        if (!PathsAreEqual(newPath, legacyPath))
        {
            MigrateIfNeeded(legacyPath, newPath);
        }

        return newPath;
    }

    private static string GetDataRoot(IWebHostEnvironment environment)
    {
        // WEBSITE_INSTANCE_ID is only set inside Azure App Service; HOME points to the
        // persistent root (D:\home on Windows, /home on Linux).
        var instanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        var home = Environment.GetEnvironmentVariable("HOME");

        if (!string.IsNullOrEmpty(instanceId) && !string.IsNullOrEmpty(home))
        {
            return Path.Combine(home, "data", AzureDataSubfolder);
        }

        // Local development fallback
        return Path.Combine(environment.ContentRootPath, LegacyRootFolder);
    }

    private static void MigrateIfNeeded(string legacyPath, string newPath)
    {
        if (!Directory.Exists(legacyPath)) return;

        var legacyFiles = Directory.GetFiles(legacyPath, "*", SearchOption.TopDirectoryOnly);
        if (legacyFiles.Length == 0) return;

        // Skip migration when the destination already has data — never overwrite
        if (Directory.GetFiles(newPath).Length > 0) return;

        foreach (var src in legacyFiles)
        {
            var dest = Path.Combine(newPath, Path.GetFileName(src));
            if (!File.Exists(dest))
            {
                File.Copy(src, dest);
            }
        }
    }

    private static bool PathsAreEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
