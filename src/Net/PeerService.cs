using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace WinPager.Net;

/// <summary>
/// Serverless LAN peer discovery and paging over UDP.
///
/// Every instance broadcasts a Hello on a timer; everyone else records the sender
/// and its endpoint. Pages are sent unicast straight to the target's endpoint and
/// answered with an Ack, so the sender can tell the user it actually landed.
/// </summary>
public sealed class PeerService : IDisposable
{
    private static readonly TimeSpan HelloInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PeerTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(3);

    private readonly Config _config;
    private readonly SynchronizationContext? _uiContext;
    private readonly ConcurrentDictionary<string, Peer> _peers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PendingPage> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _seenPageIds = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cts = new();

    /// <summary>Unique to this process run. See <see cref="PagerMessage.InstanceToken"/>.</summary>
    private readonly string _instanceToken = Guid.NewGuid().ToString("N");

    private UdpClient? _udp;
    private System.Threading.Timer? _heartbeat;
    private bool _disposed;

    private sealed record PendingPage(string PeerId, string PeerName, DateTime SentUtc);

    public PeerService(Config config)
    {
        _config = config;
        _uiContext = SynchronizationContext.Current;
    }

    /// <summary>Fires whenever the peer list changes (someone joined, left, or renamed).</summary>
    public event Action<IReadOnlyList<Peer>>? PeersChanged;

    /// <summary>Fires when someone pages this machine.</summary>
    public event Action<PagerMessage>? PageReceived;

    /// <summary>Fires when a page we sent was confirmed delivered. Argument is the target's name.</summary>
    public event Action<string>? PageDelivered;

    /// <summary>Fires when a page we sent was never acknowledged. Argument is the target's name.</summary>
    public event Action<string>? PageFailed;

    /// <summary>Fires on a networking problem worth telling the user about.</summary>
    public event Action<string>? NetworkError;

    /// <summary>Fires when this machine had to take a new device id after a clash.</summary>
    public event Action<string>? IdentityChanged;

    public IReadOnlyList<Peer> Peers =>
        _peers.Values.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

    public void Start()
    {
        try
        {
            // Deliberately no SO_REUSEADDR: if something else holds the port we want to
            // fail loudly, not silently share it and drop half the pages.
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.EnableBroadcast = true;
            socket.Bind(new IPEndPoint(IPAddress.Any, _config.Port));

            _udp = new UdpClient { Client = socket, EnableBroadcast = true };
        }
        catch (SocketException ex)
        {
            Log.Write($"Bind failed on port {_config.Port}: {ex.Message}");
            Raise(NetworkError, $"Could not open port {_config.Port}. Another program may be using it.");
            return;
        }

        _ = Task.Run(ReceiveLoopAsync);

        // Ask everyone to announce themselves right away, then settle into a heartbeat.
        Broadcast(new PagerMessage { Kind = MessageKind.Discover });
        _heartbeat = new System.Threading.Timer(
            _ => OnHeartbeat(), null, TimeSpan.Zero, HelloInterval);

        Log.Write($"Started as '{_config.DisplayName}' ({_config.DeviceId[..8]}) on port {_config.Port}");
    }

    /// <summary>Re-announce immediately, e.g. after the user renames this machine.</summary>
    public void AnnounceNow()
    {
        Broadcast(new PagerMessage { Kind = MessageKind.Hello });
        Broadcast(new PagerMessage { Kind = MessageKind.Discover });
    }

    /// <summary>Send a page to one peer. Returns false if that peer is no longer known.</summary>
    public bool SendPage(string peerId, string? text)
    {
        if (!_peers.TryGetValue(peerId, out var peer))
            return false;

        var pageId = Guid.NewGuid().ToString("N");
        var msg = new PagerMessage
        {
            Kind = MessageKind.Page,
            TargetId = peerId,
            Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            PageId = pageId,
        };

        _pending[pageId] = new PendingPage(peerId, peer.Name, DateTime.UtcNow);
        Send(msg, peer.EndPoint);

        // Retry once: UDP drops happen, and a page is cheap to repeat.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(600, _cts.Token).ConfigureAwait(false);
                if (_pending.ContainsKey(pageId))
                    Send(msg, peer.EndPoint);

                await Task.Delay(AckTimeout, _cts.Token).ConfigureAwait(false);
                if (_pending.TryRemove(pageId, out var stillPending))
                {
                    Log.Write($"Page to {stillPending.PeerName} was not acknowledged");
                    Raise(PageFailed, stillPending.PeerName);
                }
            }
            catch (OperationCanceledException)
            {
                // App is closing; nothing to report.
            }
        });

        Log.Write($"Paged {peer.Name} at {peer.EndPoint}");
        return true;
    }

    private void OnHeartbeat()
    {
        Broadcast(new PagerMessage { Kind = MessageKind.Hello });
        PruneStalePeers();
    }

    private void PruneStalePeers()
    {
        var removedAny = false;
        foreach (var peer in _peers.Values)
        {
            if (peer.IsStale(PeerTimeout) && _peers.TryRemove(peer.Id, out _))
            {
                Log.Write($"Peer timed out: {peer.Name}");
                removedAny = true;
            }
        }

        if (removedAny)
            Raise(PeersChanged, Peers);
    }

    private async Task ReceiveLoopAsync()
    {
        var udp = _udp;
        if (udp is null) return;

        while (!_cts.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException ex)
            {
                // A single bad datagram (e.g. ICMP port-unreachable) shouldn't kill the loop.
                Log.Write($"Receive error: {ex.SocketErrorCode}");
                continue;
            }

            try
            {
                HandleDatagram(result.Buffer, result.RemoteEndPoint);
            }
            catch (Exception ex)
            {
                Log.Write($"Malformed datagram from {result.RemoteEndPoint}: {ex.Message}");
            }
        }
    }

    private void HandleDatagram(byte[] buffer, IPEndPoint from)
    {
        var msg = JsonSerializer.Deserialize<PagerMessage>(Encoding.UTF8.GetString(buffer));
        if (msg is null) return;

        if (!string.Equals(msg.GroupKey, _config.GroupKey, StringComparison.Ordinal)) return;

        if (string.Equals(msg.SenderId, _config.DeviceId, StringComparison.Ordinal))
        {
            // Our own broadcast coming back to us: ignore it.
            if (string.Equals(msg.InstanceToken, _instanceToken, StringComparison.Ordinal))
                return;

            // Same device id, different process: another PC was deployed by cloning
            // this one's disk, so it inherited our identity. Without this, the two
            // would filter each other out as self and never appear in each other's
            // lists. Take a fresh identity and re-announce.
            ResolveIdentityClash(msg, from);
            return;
        }

        switch (msg.Kind)
        {
            case MessageKind.Hello:
                TouchPeer(msg, from);
                break;

            case MessageKind.Discover:
                TouchPeer(msg, from);
                Send(new PagerMessage { Kind = MessageKind.Hello }, from);
                break;

            case MessageKind.Bye:
                if (_peers.TryRemove(msg.SenderId, out var gone))
                {
                    Log.Write($"Peer left: {gone.Name}");
                    Raise(PeersChanged, Peers);
                }
                break;

            case MessageKind.Page:
                if (!string.Equals(msg.TargetId, _config.DeviceId, StringComparison.Ordinal)) return;
                TouchPeer(msg, from);

                // Always ack, even for a repeat: the first ack may be what got lost.
                Send(new PagerMessage
                {
                    Kind = MessageKind.Ack,
                    TargetId = msg.SenderId,
                    PageId = msg.PageId,
                }, from);

                // ...but only alert the user once per page. The sender retries, and UDP
                // itself can duplicate, so without this one press buzzes the desk twice.
                if (IsDuplicatePage(msg.PageId))
                {
                    Log.Write($"Ignored duplicate page from {msg.SenderName}");
                    return;
                }

                Log.Write($"Paged by {msg.SenderName}");
                Raise(PageReceived, msg);
                break;

            case MessageKind.Ack:
                if (msg.PageId is not null && _pending.TryRemove(msg.PageId, out var pending))
                    Raise(PageDelivered, pending.PeerName);
                break;
        }
    }

    /// <summary>
    /// Give this machine a new device id after discovering another machine using the
    /// same one. Both sides do this, so whichever order they notice in, they end up
    /// with distinct identities within a heartbeat.
    /// </summary>
    private void ResolveIdentityClash(PagerMessage msg, IPEndPoint from)
    {
        var old = _config.DeviceId;
        _config.DeviceId = Guid.NewGuid().ToString("N");
        _config.Save();

        Log.Write($"Device id clash with {msg.SenderName} at {from} " +
                  $"(both were {old[..8]}); took the new id {_config.DeviceId[..8]}");

        Raise(IdentityChanged, _config.DeviceId);
        AnnounceNow();
    }

    /// <summary>True if we've already alerted for this page id. Also prunes old ids.</summary>
    private bool IsDuplicatePage(string? pageId)
    {
        if (pageId is null) return false;

        var now = DateTime.UtcNow;
        foreach (var (id, seenAt) in _seenPageIds)
        {
            if (now - seenAt > TimeSpan.FromMinutes(2))
                _seenPageIds.TryRemove(id, out _);
        }

        return !_seenPageIds.TryAdd(pageId, now);
    }

    private void TouchPeer(PagerMessage msg, IPEndPoint from)
    {
        var isNewOrRenamed = false;

        _peers.AddOrUpdate(
            msg.SenderId,
            _ =>
            {
                isNewOrRenamed = true;
                Log.Write($"Peer discovered: {msg.SenderName} at {from}");
                return new Peer
                {
                    Id = msg.SenderId,
                    Name = msg.SenderName,
                    EndPoint = from,
                    LastSeenUtc = DateTime.UtcNow,
                };
            },
            (_, existing) =>
            {
                if (!string.Equals(existing.Name, msg.SenderName, StringComparison.Ordinal))
                {
                    existing.Name = msg.SenderName;
                    isNewOrRenamed = true;
                }
                existing.EndPoint = from;
                existing.LastSeenUtc = DateTime.UtcNow;
                return existing;
            });

        if (isNewOrRenamed)
            Raise(PeersChanged, Peers);
    }

    private void Broadcast(PagerMessage msg)
    {
        foreach (var target in BroadcastTargets())
            Send(msg, target);
    }

    private void Send(PagerMessage msg, IPEndPoint to)
    {
        var udp = _udp;
        if (udp is null || _disposed) return;

        msg.SenderId = _config.DeviceId;
        msg.SenderName = _config.DisplayName;
        msg.GroupKey = _config.GroupKey;
        msg.InstanceToken = _instanceToken;

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(msg);
            udp.Send(bytes, bytes.Length, to);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            Log.Write($"Send to {to} failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Limited broadcast plus each interface's directed broadcast, so machines on a
    /// second NIC or a Wi-Fi/Ethernet split still hear us.
    /// </summary>
    private List<IPEndPoint> BroadcastTargets()
    {
        var targets = new List<IPEndPoint> { new(IPAddress.Broadcast, _config.Port) };

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var info in nic.GetIPProperties().UnicastAddresses)
                {
                    if (info.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (info.IPv4Mask is null) continue;

                    var addr = info.Address.GetAddressBytes();
                    var mask = info.IPv4Mask.GetAddressBytes();
                    var broadcast = new byte[4];
                    for (var i = 0; i < 4; i++)
                        broadcast[i] = (byte)(addr[i] | ~mask[i]);

                    targets.Add(new IPEndPoint(new IPAddress(broadcast), _config.Port));
                }
            }
        }
        catch (NetworkInformationException ex)
        {
            Log.Write($"Could not enumerate interfaces: {ex.Message}");
        }

        return targets.DistinctBy(e => e.ToString()).ToList();
    }

    /// <summary>Marshal an event back to the UI thread so handlers can touch controls safely.</summary>
    private void Raise<T>(Action<T>? handler, T arg)
    {
        if (handler is null) return;

        if (_uiContext is not null)
            _uiContext.Post(_ => handler(arg), null);
        else
            handler(arg);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { Broadcast(new PagerMessage { Kind = MessageKind.Bye }); } catch { /* shutting down */ }

        _cts.Cancel();
        _heartbeat?.Dispose();
        _udp?.Dispose();
        _cts.Dispose();
    }
}
