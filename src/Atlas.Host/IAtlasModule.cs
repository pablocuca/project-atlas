using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Atlas.Host;

// docs/03-architecture/03-modular-monolith.md §5. Adding a module is meant to be a one-line change
// in the host — this is what that one line implements against.
public interface IAtlasModule
{
    string Name { get; }
    string Version { get; }

    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    void RegisterEventHandlers(IEventBusBuilder eventBus);

    void RegisterEndpoints(IEndpointRouteBuilder endpoints);

    IReadOnlyList<Migration> Migrations { get; }

    IReadOnlyList<IHealthCheck> HealthChecks { get; }
}

public sealed record Migration(string Name, string Sql);

// A minimal marker for RegisterEventHandlers to target. The full outbox-backed IEventBus
// (modular-monolith.md §6: shared.outbox, transactional publish, a background dispatcher) has
// nothing to do yet — there is exactly one module, and JournalEntryPosted has no subscriber. This
// exists so the module contract compiles and means something the day a second module needs it.
public interface IEventBusBuilder;
