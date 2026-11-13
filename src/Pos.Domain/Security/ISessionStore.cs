using Pos.Domain.Models;

namespace Pos.Domain.Security;

/// <summary>The logged-in session lives in application memory only.</summary>
public interface ISessionStore
{
    UserSession? Current { get; }

    void Set(UserSession session);

    void Clear();
}
