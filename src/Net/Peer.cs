using System.Net;

namespace ChabadOfficePager.Net;

/// <summary>Another machine we've heard from recently.</summary>
public sealed class Peer
{
    public required string Id { get; init; }
    public string Name { get; set; } = "";
    public IPEndPoint EndPoint { get; set; } = new(IPAddress.None, 0);
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

    public bool IsStale(TimeSpan timeout) => DateTime.UtcNow - LastSeenUtc > timeout;
}
