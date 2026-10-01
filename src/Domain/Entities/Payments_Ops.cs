using SmartRestaurant.Domain.Common;

namespace SmartRestaurant.Domain.Entities;

public class Payment : BaseEntity, IBranchScoped
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public PaymentGatewayType Gateway { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? Authority { get; set; }                   // gateway ref before redirect
    public string? RefId { get; set; }                       // tracking number after verify
    public string? CardPanMasked { get; set; }
    public string? GatewayResponse { get; set; }             // raw json for audit
    public DateTime? PaidAt { get; set; }
    public Guid? BranchId { get; set; }
}

/// <summary>Per-gateway merchant config, editable by SuperAdmin at runtime.</summary>
public class PaymentGatewayConfig : BaseEntity
{
    public PaymentGatewayType Gateway { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Sandbox { get; set; } = true;
    public string? MerchantKey { get; set; }                 // merchant id / api key
    public string? Secret { get; set; }
    public string? Extra { get; set; }
}

public class PrintJob : BaseEntity, IBranchScoped
{
    public PrinterType PrinterType { get; set; }
    public Guid? PrinterId { get; set; }
    public Guid? OrderId { get; set; }
    public string Content { get; set; } = default!;          // raw ESC/POS bytes (base64)
    public string? PlainText { get; set; }                   // human readable for log/debug
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int Attempts { get; set; }
    public Guid? BranchId { get; set; }
}

public class PrinterConfig : BaseEntity, IBranchScoped
{
    public string Name { get; set; } = default!;
    public PrinterType Type { get; set; }
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 9100;
    public bool IsDefault { get; set; }
    public bool UseRasterMode { get; set; } = true;          // image mode = Persian-safe
    public string Encoding { get; set; } = "utf-8";          // text-mode fallback codepage
    public Guid? BranchId { get; set; }
}

public class Camera : BaseEntity, IBranchScoped
{
    public string Name { get; set; } = default!;
    public CameraType Type { get; set; } = CameraType.Demo;
    public string Url { get; set; } = default!;              // demo:*, http(s) mjpeg, rtsp://, .m3u8
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? BranchId { get; set; }
}
