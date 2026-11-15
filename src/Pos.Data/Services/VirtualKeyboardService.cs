using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Security;
using Pos.Domain.Time;
using System.Threading;
using System.Threading.Tasks;

namespace Pos.Data.Services;

/// <summary>
/// Stub service for virtual keyboard (on-screen keyboard).
/// When enabled, shows an on-screen keyboard for touch devices.
/// </summary>
public sealed class VirtualKeyboardService
{
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public VirtualKeyboardService(ISessionStore sessions, IClock clock)
    {
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        // Virtual keyboard toggle checked via settings in UI layer
        await Task.CompletedTask.ConfigureAwait(false);
        return false;
    }

    public void ShowKeyboard()
    {
        // Stub: in production, this would show an on-screen keyboard
    }

    public void HideKeyboard()
    {
        // Stub: hide on-screen keyboard
    }
}
