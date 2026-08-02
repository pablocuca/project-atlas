using Atlas.Kernel;
using Atlas.Modules.Cashflow.Application;
using Atlas.Modules.Cashflow.Domain;
using Npgsql;

namespace Atlas.Modules.Cashflow.Infrastructure;

public sealed class ClassificationRepository(NpgsqlDataSource dataSource) : IClassificationRepository
{
    public async Task RecordAsync(ClassificationDecision decision, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO cashflow.classification_decision
                (decision_id, tenant_id, category_account_id, classification, rationale, decided_at)
            VALUES (@decisionId, @tenantId, @categoryAccountId, @classification, @rationale, @decidedAt)
            """);

        command.Parameters.AddWithValue("decisionId", decision.Id.Value);
        command.Parameters.AddWithValue("tenantId", decision.TenantId.Value);
        command.Parameters.AddWithValue("categoryAccountId", decision.CategoryAccountId);
        command.Parameters.AddWithValue("classification", decision.Classification.ToString());
        command.Parameters.AddWithValue("rationale", (object?)decision.Rationale ?? DBNull.Value);
        command.Parameters.AddWithValue("decidedAt", decision.DecidedAt);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClassificationDecision>> FindHistoryAsync(
        TenantId tenantId, Guid categoryAccountId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT decision_id, classification, rationale, decided_at
            FROM cashflow.classification_decision
            WHERE tenant_id = @tenantId AND category_account_id = @categoryAccountId
            ORDER BY decided_at
            """);
        command.Parameters.AddWithValue("tenantId", tenantId.Value);
        command.Parameters.AddWithValue("categoryAccountId", categoryAccountId);

        var history = new List<ClassificationDecision>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            history.Add(new ClassificationDecision(
                new ClassificationDecisionId(reader.GetGuid(0)), tenantId, categoryAccountId,
                Enum.Parse<Classification>(reader.GetString(1)),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3)));
        }

        return history;
    }
}
