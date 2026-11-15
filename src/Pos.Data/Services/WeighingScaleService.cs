using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Security;
using Pos.Domain.Time;
using System.Threading;
using System.Threading.Tasks;

namespace Pos.Data.Services;

/// <summary>
/// Stub service for weighing scale integration.
/// When enabled, connects to a weighing scale for weighed products.
/// </summary>
public sealed class WeighingScaleService
{
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public WeighingScaleService(ISessionStore sessions, IClock clock)
    {
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return false;
    }

    public async Task<long> ReadWeightAsync(CancellationToken cancellationToken = default)
    {
        // Stub: returns 0 (no scale connected)
        await Task.CompletedTask.ConfigureAwait(false);
        return 0;
    }
}
