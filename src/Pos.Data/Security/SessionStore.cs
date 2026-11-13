using Pos.Domain.Models;
using Pos.Domain.Security;

namespace Pos.Data.Security;

public sealed class SessionStore : ISessionStore
{
    private UserSession? _current;

    public UserSession? Current => Volatile.Read(ref _current);

    public void Set(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Volatile.Write(ref _current, session);
    }

    public void Clear() => Volatile.Write(ref _current, null);
}
