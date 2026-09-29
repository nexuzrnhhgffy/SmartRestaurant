using System.Text.Json;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;

namespace SmartRestaurant.Application.Payments;

public class GatewayRequest
{
    public decimal AmountToman { get; set; }
    public string CallbackUrl { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string OrderNumber { get; set; } = default!;
    public string Mobile { get; set; } = default!;
}

public class GatewayCreateResult
{
    public bool Success { get; set; }
    public string? Authority { get; set; }      // token/authority to build the redirect
    public string? RedirectUrl { get; set; }    // full bank URL (or null → build via BuildRedirect)
    public string? Error { get; set; }
    public string? Raw { get; set; }
}

public class GatewayVerifyInput
{
    public string Authority { get; set; } = default!;
    public decimal AmountToman { get; set; }
    public Dictionary<string, string> CallbackParams { get; set; } = new();
}

public class GatewayVerifyResult
{
    public bool Success { get; set; }
    public bool AlreadyVerified { get; set; }
    public string? RefId { get; set; }
    public string? CardPanMasked { get; set; }
    public string? Error { get; set; }
    public string? Raw { get; set; }
}

/// <summary>Contract every Iranian PSP implements. Amounts are Toman (÷10 for Rial APIs where needed).</summary>
public interface IPaymentGateway
{
    PaymentGatewayType Type { get; }
    string DisplayName { get; }
    Task<GatewayCreateResult> CreateAsync(GatewayRequest request, PaymentGatewayConfig cfg, CancellationToken ct = default);
    Task<GatewayVerifyResult> VerifyAsync(GatewayVerifyInput input, PaymentGatewayConfig cfg, CancellationToken ct = default);
}

public abstract class RestGatewayBase : IPaymentGateway
{
    public abstract PaymentGatewayType Type { get; }
    public abstract string DisplayName { get; }
    protected static readonly HttpClient Http = CreateClient();
    protected static readonly JsonSerializerOptions J = new() { PropertyNameCaseInsensitive = true };

    protected static HttpClient CreateClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("SmartRestaurant/1.0");
        c.Timeout = TimeSpan.FromSeconds(30);
        return c;
    }

    public abstract Task<GatewayCreateResult> CreateAsync(GatewayRequest request, PaymentGatewayConfig cfg, CancellationToken ct = default);
    public abstract Task<GatewayVerifyResult> VerifyAsync(GatewayVerifyInput input, PaymentGatewayConfig cfg, CancellationToken ct = default);

    protected static async Task<JsonElement> PostJsonAsync(string url, object body, Dictionary<string, string>? headers = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json") };
        if (headers != null) foreach (var h in headers) req.Headers.TryAddWithoutValidation(h.Key, h.Value);
        using var res = await Http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}

/// <summary>Zarinpal PG v4 — https://docs.zarinpal.com
/// Request:  POST https://api.zarinpal.com/pg/v4/payment/request.json
///           { "merchant_id", "amount"(Rial), "callback_url", "description" } → data.authority (code 100)
/// StartPay: https://www.zarinpal.com/pg/StartPay/{authority}
/// Verify:   POST https://api.zarinpal.com/pg/v4/payment/verify.json → data.code 100 ok / 101 already, data.ref_id
/// </summary>
public class ZarinpalGateway : RestGatewayBase
{
    public override PaymentGatewayType Type => PaymentGatewayType.Zarinpal;
    public override string DisplayName => "Zarinpal (زرین‌پال)";
    private const string ApiBase = "https://api.zarinpal.com/pg/v4/payment";
    private const string StartPay = "https://www.zarinpal.com/pg/StartPay/";

    public override async Task<GatewayCreateResult> CreateAsync(GatewayRequest r, PaymentGatewayConfig cfg, CancellationToken ct = default)
    {
        try
        {
            var body = new { merchant_id = cfg.MerchantKey, amount = (long)(r.AmountToman * 10), callback_url = r.CallbackUrl, description = r.Description, metadata = new { order_id = r.OrderNumber, mobile = r.Mobile } };
            var json = await PostJsonAsync($"{ApiBase}/request.json", body, ct: ct);
            var data = json.GetProperty("data");
            var code = data.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
            if (code == 100 && data.TryGetProperty("authority", out var auth))
            {
                var authority = auth.GetString()!;
                return new GatewayCreateResult { Success = true, Authority = authority, RedirectUrl = StartPay + authority, Raw = json.GetRawText() };
            }
            var errors = json.TryGetProperty("errors", out var e) ? e.GetRawText() : "unknown";
            return new GatewayCreateResult { Success = false, Error = $"Zarinpal code={code}: {errors}", Raw = json.GetRawText() };
        }
        catch (Exception ex) { return new GatewayCreateResult { Success = false, Error = ex.Message }; }
    }

    public override async Task<GatewayVerifyResult> VerifyAsync(GatewayVerifyInput input, PaymentGatewayConfig cfg, CancellationToken ct = default)
    {
        try
        {
            var body = new { merchant_id = cfg.MerchantKey, amount = (long)(input.AmountToman * 10), authority = input.Authority };
            var json = await PostJsonAsync($"{ApiBase}/verify.json", body, ct: ct);
            var data = json.GetProperty("data");
            var code = data.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
            if (code is 100 or 101)
            {
                var refId = data.TryGetProperty("ref_id", out var rf) ? rf.GetString() : null;
                var pan = data.TryGetProperty("card_pan", out var cp) ? cp.GetString() : null;
                return new GatewayVerifyResult { Success = true, AlreadyVerified = code == 101, RefId = refId, CardPanMasked = pan, Raw = json.GetRawText() };
            }
            return new GatewayVerifyResult { Success = false, Error = $"Zarinpal verify code={code}", Raw = json.GetRawText() };
        }
        catch (Exception ex) { return new GatewayVerifyResult { Success = false, Error = ex.Message }; }
    }
}

/// <summary>Zibal — https://docs.zibal.ir
/// Request: POST https://gateway.zibal.ir/v1/request {merchant, amount(Rial), callbackUrl, orderId} → {trackId, result:100}
/// Start:   https://gateway.zibal.ir/start/{trackId}
/// Verify:  POST https://gateway.zibal.ir/v1/verify {merchant, trackId} → result 100 paid / 201 already
/// </summary>
public class ZibalGateway : RestGatewayBase
{
    public override PaymentGatewayType Type => PaymentGatewayType.Zibal;
    public override string DisplayName => "Zibal (زیبال)";
    private const string ApiBase = "https://gateway.zibal.ir/v1";

    public override async Task<GatewayCreateResult> CreateAsync(GatewayRequest r, PaymentGatewayConfig cfg, CancellationToken ct = default)
    {
        try
        {
            var body = new { merchant = string.IsNullOrWhiteSpace(cfg.MerchantKey) ? "zibal" : cfg.MerchantKey, amount = (long)(r.AmountToman * 10), callbackUrl = r.CallbackUrl, orderId = r.OrderNumber, description = r.Description };
            var json = await PostJsonAsync($"{ApiBase}/request", body, ct: ct);
            var result = json.TryGetProperty("result", out var rr) ? rr.GetInt32() : 0;
            if (result == 100 && json.TryGetProperty("trackId", out var t))
            {
                var track = t.GetString() ?? t.GetInt64().ToString();
                return new GatewayCreateResult { Success = true, Authority = track, RedirectUrl = $"https://gateway.zibal.ir/start/{track}", Raw = json.GetRawText() };
            }
            var msg = json.TryGetProperty("message", out var m) ? m.GetString() : null;
            return new GatewayCreateResult { Success = false, Error = $"Zibal result={result} {msg}", Raw = json.GetRawText() };
        }
        catch (Exception ex) { return new GatewayCreateResult { Success = false, Error = ex.Message }; }
    }

    public override async Task<GatewayVerifyResult> VerifyAsync(GatewayVerifyInput input, PaymentGatewayConfig cfg, CancellationToken ct = default)
    {
        try
        {
            var body = new { merchant = string.IsNullOrWhiteSpace(cfg.MerchantKey) ? "zibal" : cfg.MerchantKey, trackId = input.Authority };
            var json = await PostJsonAsync($"{ApiBase}/verify", body, ct: ct);
            var result = json.TryGetProperty("result", out var rr) ? rr.GetInt32() : 0;
            if (result is 100 or 201)
            {
                json.TryGetProperty("cardNumber", out var pan);
                return new GatewayVerifyResult { Success = true, AlreadyVerified = result == 201, RefId = input.Authority, CardPanMasked = pan.ValueKind == JsonValueKind.String ? pan.GetString() : null, Raw = json.GetRawText() };
            }
            return new GatewayVerifyResult { Success = false, Error = $"Zibal verify result={result}", Raw = json.GetRawText() };
        }
        catch (Exception ex) { return new GatewayVerifyResult { Success = false, Error = ex.Message }; }
    }
}

/// <summary>IDPay — https://idpay.ir/web-service
/// Headers: X-API-KEY, X-SANDBOX: 0|1
/// Request: POST https://api.idpay.ir/v1.1/payment {order_id, amount(Rial), callback} → {id, link}
/// Verify:  POST https://api.idpay.ir/v1.1/payment/verify {id, order_id} → status 100 ok / 101 already, track_id
/// </summary>
public class IdPayGateway : RestGatewayBase
{
    public override PaymentGatewayType Type => PaymentGatewayType.IDPay;
    public override string DisplayName => "IDPay (آی‌دی‌پی)";
    private const string ApiBase = "https://api.idpay.ir/v1.1";

    public override async Task<GatewayCreateResult> CreateAsync(GatewayRequest r, PaymentGatewayConfig cfg, CancellationToken ct = default)
    {
        try
        {
            var headers = new Dictionary<string, string> { ["X-API-KEY"] = cfg.MerchantKey ?? "", ["X-SANDBOX"] = cfg.Sandbox ? "1" : "0" };
            var body = new { order_id = r.OrderNumber, amount = (long)(r.AmountToman * 10), callback = r.CallbackUrl };
            var json = await PostJsonAsync($"{ApiBase}/payment", body, headers, ct);
            if (json.TryGetProperty("id", out var id) && json.TryGetProperty("link", out var link))
                return new GatewayCreateResult { Success = true, Authority = id.GetString(), RedirectUrl = link.GetString(), Raw = json.GetRawText() };
            var em = json.TryGetProperty("error_message", out var e) ? e.GetString() : "IDPay create failed";
            return new GatewayCreateResult { Success = false, Error = em, Raw = json.GetRawText() };
        }
        catch (Exception ex) { return new GatewayCreateResult { Success = false, Error = ex.Message }; }
    }

    public override async Task<GatewayVerifyResult> VerifyAsync(GatewayVerifyInput input, PaymentGatewayConfig cfg, CancellationToken ct = default)
    {
        try
        {
            var headers = new Dictionary<string, string> { ["X-API-KEY"] = cfg.MerchantKey ?? "", ["X-SANDBOX"] = cfg.Sandbox ? "1" : "0" };
            var body = new { id = input.Authority, order_id = input.CallbackParams.TryGetValue("order_id", out var o) ? o : "" };
            var json = await PostJsonAsync($"{ApiBase}/payment/verify", body, headers, ct);
            if (json.TryGetProperty("status", out var st))
            {
                var status = st.ValueKind == JsonValueKind.Number ? st.GetInt32() : int.Parse(st.GetString()!);
                if (status is 100 or 101)
                {
                    json.TryGetProperty("track_id", out var tr);
                    json.TryGetProperty("card_no", out var pan);
                    return new GatewayVerifyResult
                    {
                        Success = true, AlreadyVerified = status == 101,
                        RefId = tr.ValueKind == JsonValueKind.Number ? tr.GetInt64().ToString() : tr.GetString(),
                        CardPanMasked = pan.ValueKind == JsonValueKind.String ? pan.GetString() : null,
                        Raw = json.GetRawText()
                    };
                }
            }
            return new GatewayVerifyResult { Success = false, Error = $"IDPay status not success", Raw = json.GetRawText() };
        }
        catch (Exception ex) { return new GatewayVerifyResult { Success = false, Error = ex.Message }; }
    }
}

/// <summary>Pay.ir — https://docs.pay.ir
/// Request: POST https://pay.ir/pg/send {api, amount(Rial), redirect, description} → {status:1, token}
/// Gateway: https://pay.ir/pg/{token}
/// Verify:  POST https://pay.ir/pg/verify {api, token} → {status:1, transId}
/// </summary>
public class PayIrGateway : RestGatewayBase
{
    public override PaymentGatewayType Type => PaymentGatewayType.PayIr;
    public override string DisplayName => "Pay.ir (پی‌آی‌آر)";
    private const string ApiBase = "https://pay.ir/pg";

    public override async Task<GatewayCreateResult> CreateAsync(GatewayRequest r, PaymentGatewayConfig cfg, CancellationToken ct = default)
    {
        try
        {
            var body = new { api = cfg.MerchantKey ?? "test", amount = (long)(r.AmountToman * 10), redirect = r.CallbackUrl, description = r.Description, mobile = r.Mobile };
            var json = await PostJsonAsync($"{ApiBase}/send", body, ct: ct);
            if (json.TryGetProperty("status", out var s) && s.GetInt32() == 1 && json.TryGetProperty("token", out var t))
            {
                var token = t.GetString()!;
                return new GatewayCreateResult { Success = true, Authority = token, RedirectUrl = $"{ApiBase}/{token}", Raw = json.GetRawText() };
            }
            var em = json.TryGetProperty("errorMessage", out var e) ? e.GetString() : "Pay.ir create failed";
            return new GatewayCreateResult { Success = false, Error = em, Raw = json.GetRawText() };
        }
        catch (Exception ex) { return new GatewayCreateResult { Success = false, Error = ex.Message }; }
    }

    public override async Task<GatewayVerifyResult> VerifyAsync(GatewayVerifyInput input, PaymentGatewayConfig cfg, CancellationToken ct = default)
    {
        try
        {
            var body = new { api = cfg.MerchantKey ?? "test", token = input.Authority, refId = "" };
            var json = await PostJsonAsync($"{ApiBase}/verify", body, ct: ct);
            if (json.TryGetProperty("status", out var s) && s.GetInt32() == 1)
            {
                json.TryGetProperty("transId", out var tr);
                json.TryGetProperty("cardNumber", out var pan);
                return new GatewayVerifyResult
                {
                    Success = true, AlreadyVerified = false,
                    RefId = tr.ValueKind == JsonValueKind.Number ? tr.GetInt64().ToString() : tr.GetString(),
                    CardPanMasked = pan.ValueKind == JsonValueKind.String ? pan.GetString() : null,
                    Raw = json.GetRawText()
                };
            }
            return new GatewayVerifyResult { Success = false, Error = "Pay.ir verify failed", Raw = json.GetRawText() };
        }
        catch (Exception ex) { return new GatewayVerifyResult { Success = false, Error = ex.Message }; }
    }
}

/// <summary>NextPay — https://nextpay.org
/// Request: POST https://nextpay.org/nx/gateway/token {api_key, order_id, amount(Rial), callback_uri} → {code:-1, trans_id}
/// Gateway: https://nextpay.org/nx/gateway/payment/{trans_id}
/// Verify:  POST https://nextpay.org/nx/gateway/verify {api_key, trans_id, amount} → code 0 ok
/// </summary>
public class NextPayGateway : RestGatewayBase
{
    public override PaymentGatewayType Type => PaymentGatewayType.NextPay;
    public override string DisplayName => "NextPay (نکست‌پی)";
    private const string ApiBase = "https://nextpay.org/nx/gateway";

    public override async Task<GatewayCreateResult> CreateAsync(GatewayRequest r, PaymentGatewayConfig cfg, CancellationToken ct = default)
    {
        try
        {
            var body = new { api_key = cfg.MerchantKey, order_id = r.OrderNumber, amount = (long)(r.AmountToman * 10), callback_uri = r.CallbackUrl };
            var json = await PostJsonAsync($"{ApiBase}/token", body, ct: ct);
            var code = json.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
            if (code == -1 && json.TryGetProperty("trans_id", out var t))
            {
                var trans = t.GetString()!;
                return new GatewayCreateResult { Success = true, Authority = trans, RedirectUrl = $"{ApiBase}/payment/{trans}", Raw = json.GetRawText() };
            }
            return new GatewayCreateResult { Success = false, Error = $"NextPay code={code}", Raw = json.GetRawText() };
        }
        catch (Exception ex) { return new GatewayCreateResult { Success = false, Error = ex.Message }; }
    }

    public override async Task<GatewayVerifyResult> VerifyAsync(GatewayVerifyInput input, PaymentGatewayConfig cfg, CancellationToken ct = default)
    {
        try
        {
            var body = new { api_key = cfg.MerchantKey, trans_id = input.Authority, amount = (long)(input.AmountToman * 10), currency = "IRR" };
            var json = await PostJsonAsync($"{ApiBase}/verify", body, ct: ct);
            var code = json.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
            if (code is 0 or -4) // 0 = ok, -4 = already verified
                return new GatewayVerifyResult { Success = true, AlreadyVerified = code == -4, RefId = input.Authority, Raw = json.GetRawText() };
            return new GatewayVerifyResult { Success = false, Error = $"NextPay verify code={code}", Raw = json.GetRawText() };
        }
        catch (Exception ex) { return new GatewayVerifyResult { Success = false, Error = ex.Message }; }
    }
}
