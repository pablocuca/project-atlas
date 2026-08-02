using Atlas.Kernel;
using Atlas.Modules.Positions.Domain;

namespace Atlas.Modules.Positions.Application;

public interface IPositionRepository
{
    Task<Position?> FindAsync(TenantId tenantId, Commodity commodity, CancellationToken cancellationToken);

    // Positions are a projection over the Ledger, fully rebuildable (ADR-0018) — Replace atomically
    // swaps a tenant+commodity's entire Lot/Disposal state for the freshly recomputed one; there is
    // no incremental append path, because the projection is never independently authored.
    Task ReplaceAsync(Position position, CancellationToken cancellationToken);
}
