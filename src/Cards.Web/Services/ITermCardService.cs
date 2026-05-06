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
        string? imageDataUrl,
        string? audio1Base64 = null,
        CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<int> GetTotalCountAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TermCard>> GetAllByUserAsync(string userId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetAllIdsAsync(CancellationToken ct = default);
}
