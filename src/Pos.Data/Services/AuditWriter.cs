using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Domain;

namespace Pos.Data.Services;

internal static class AuditWriter
{
    public static Task WriteAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string? actorUserId,
        string action,
        string entityType,
        string? entityId,
        object? details,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO audit_events
              (id, actor_user_id, action, entity_type, entity_id, details_json, created_at)
            VALUES
              (@id, @actorUserId, @action, @entityType, @entityId, @detailsJson, @createdAt);
            """;

        return connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                id = Ids.NewId(),
                actorUserId,
                action,
                entityType,
                entityId,
                detailsJson = details is null ? "{}" : SqlText.Serialize(details),
                createdAt = SqlText.Now(now)
            },
            transaction,
            cancellationToken: cancellationToken));
    }
}
