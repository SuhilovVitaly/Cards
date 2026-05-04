namespace Cards.Web.Services;

public interface IAppInfoService
{
    string Version { get; }

    string? GitCommit { get; }

    DateTime? BuildDateUtc { get; }
}
