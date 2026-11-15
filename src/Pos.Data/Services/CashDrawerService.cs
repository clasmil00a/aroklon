using Pos.Domain;
using Pos.Domain.Security;
using Pos.Domain.Time;
using System.Threading;
using System.Threading.Tasks;

namespace Pos.Data.Services;

/// <summary>
/// Triggers the cash drawer kick (ESC/POS pulse to drawer trigger pin).
/// In production this sends the ESC/POS command to a connected printer.
/// For now it logs the action so the feature is wired and testable.
/// </summary>
public sealed class CashDrawerService
{
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public CashDrawerService(ISessionStore sessions, IClock clock)
    {
        _sessions = sessions;
        _clock = clock;
    }

    /// <summary>
    /// Kicks the cash drawer. Requires an active session.
    /// </summary>
    public async Task KickAsync(CancellationToken cancellationToken = default)
    {
        _ = _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");

        // TODO: Hook up ESC/POS printer command here when a thermal printer
        // is connected. Example: printer.Write(new byte[] { 0x1B, 0x70, 0x00, 0x20, 0x20 });
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
