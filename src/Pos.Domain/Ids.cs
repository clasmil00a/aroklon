namespace Pos.Domain;

/// <summary>Application-generated UUID v7 identifiers, stored as TEXT.</summary>
public static class Ids
{
    public static string NewId() => Guid.CreateVersion7().ToString("D");
}
