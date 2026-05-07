using Cards.Web.Models;

namespace Cards.Web.Services;

public interface ICollectionService
{
    Task<IReadOnlyList<Collection>> GetAllAsync(string userId, CancellationToken ct = default);

    Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken ct = default);

    Task<Collection?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Collection> CreateAsync(string userId, string name, Language lang1, Language lang2, CancellationToken ct = default);

    Task RenameAsync(Guid id, string name, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task AddCardAsync(Guid collectionId, Guid cardId, CancellationToken ct = default);

    Task RemoveCardAsync(Guid collectionId, Guid cardId, CancellationToken ct = default);

    Task<int> GetTotalCountAsync(CancellationToken ct = default);
}
