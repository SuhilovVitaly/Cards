using Cards.Web.Models;

namespace Cards.Web.Services;

public interface ITermCardService
{
    Task<TermCard?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<List<TermCard>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    Task<TermCard> CreateAsync(
        string userId,
        Language lang1,
        Language lang2,
        string text1,
        string text2,
        string? image1DataUrl,
        string? image2DataUrl,
        CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<int> GetTotalCountAsync(CancellationToken ct = default);
}
