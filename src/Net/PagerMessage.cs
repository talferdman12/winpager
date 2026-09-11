using System.Text.Json.Serialization;

namespace WinPager.Net;

public enum MessageKind
{
    Unknown = 0,
    /// <summary>"I'm here" heartbeat, broadcast on a timer.</summary>
    Hello,
    /// <summary>"Who's there?" — asks everyone to send a Hello immediately.</summary>
    Discover,
    /// <summary>Graceful shutdown notice.</summary>
    Bye,
    /// <summary>An actual page directed at one device.</summary>
    Page,
    /// <summary>Receipt confirmation so the sender knows it landed.</summary>
    Ack,
}

/// <summary>Wire format. Kept flat and small so it always fits in one UDP datagram.</summary>
public sealed class PagerMessage
{
    [JsonPropertyName("k")] public MessageKind Kind { get; set; }
    [JsonPropertyName("g")] public string GroupKey { get; set; } = "";
    [JsonPropertyName("id")] public string SenderId { get; set; } = "";
    [JsonPropertyName("n")] public string SenderName { get; set; } = "";
    /// <summary>Target device id. Only meaningful for Page and Ack.</summary>
    [JsonPropertyName("to")] public string? TargetId { get; set; }
    /// <summary>Optional note typed by the sender.</summary>
    [JsonPropertyName("m")] public string? Text { get; set; }
    /// <summary>Unique id for one page, echoed back in the Ack.</summary>
    [JsonPropertyName("pid")] public string? PageId { get; set; }
}
