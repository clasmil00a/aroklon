namespace Pos.Domain.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
