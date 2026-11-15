using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Security;
using Pos.Domain.Time;
using System.Threading;
using System.Threading.Tasks;

namespace Pos.Data.Services;

/// <summary>
/// Stub service for multi-register shared database (Pro).
/// When enabled, allows multiple registers on one database.
/// </summary>
public sealed class MultiRegisterService
{
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public MultiRegisterService(ISessionStore sessions, IClock clock)
    {
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return false;
    }

    public async Task<IReadOnlyList<MultiRegisterInfo>> GetRegistersAsync(
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new List<MultiRegisterInfo>();
    }
}

/// <summary>Register info for multi-register setup.</summary>
public sealed record MultiRegisterInfo(string Id, string Name, bool IsOnline, string LastSeen);
