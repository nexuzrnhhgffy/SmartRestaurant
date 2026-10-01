using System.Text;
using SmartRestaurant.Domain.Common;
using System.Text.Json;
using SmartRestaurant.Domain.Entities;

namespace SmartRestaurant.Application.Payments;

/*
 ═══════════════════════════════════════════════════════════════════════════
 SHAPARAK DIRECT BANK GATEWAYS (Mellat / Saman / Parsian / Pasargad / Novin)
 ═══════════════════════════════════════════════════════════════════════════
 These PSPs expose SOAP (BPMPayRequest / bpPayRequest) or SOAP-like XML APIs.
 They require a Shaparak-registered terminal (TERMINAL_ID + USERNAME + PASSWORD)
 and usually need to be whitelisted for your server IP.

 This project ships them as *documented sandbox stubs* implementing the same
 IPaymentGateway contract, so the whole payment pipeline (initiate → redirect →
 callback → verify → ledger) works end-to-end today and you only need to drop
 the real SOAP envelope in when your terminal credentials arrive.

 How to integrate for real (Mellat BPM example):
   1. Add WSDL ref: https://bpm.shaparak.ir/pgwchannel/services/pgw?wsdl
   2. bpPayRequest(terminalId, userName, password, orderId, amount(Rial),
        localDate(yyyyMMdd), localTime(HHmmss), additionalData, callBackUrl, payerId)
      → returns "0,RefId" → redirect to https://bpm.shaparak.ir/pgwchannel/startpay.mellat?RefId=...
   3. Callback posts RefId, ResCode, SaleOrderId, SaleReferenceId, CardHolderPan
   4. bpVerifyRequest + bpSettleRequest with the same SaleReferenceId.

 Saman  : https://sep.shaparak.ir/Payments/InitPayment.asmx (TokenRequest → URLtokenPayment → verifyTransaction)
 Parsian: https://pec.shaparak.ir/NewIPGServices/Sale/SaleService.svc (SalePaymentRequest → ConfirmPaymentRequest)
 Pasargad: RSA-signed POST https://pep.shaparak.ir/Api/v1/Payment/GetToken (needs private key XML in cfg.Secret)
 Novin  : https://novinypay.ir/payment/gateway-send (SOAP 1.2, similar to Saman)
 All amounts in Rial → we pass AmountToman * 10, matching the other gateways.
═══════════════════════════════════════════════════════════════════════════ */

public abstract class BankSoapGatewayBase : IPaymentGateway
{
    public abstract PaymentGatewayType Type { get; }
    public abstract string DisplayName { get; }
    protected abstract string SandboxPrefix { get; }

    public virtual Task<GatewayCreateResult> CreateAsync(GatewayRequest r, PaymentGatewayConfig cfg, CancellationToken ct = default)
    {
        // Sandbox behaviour: issue a synthetic authority that the internal sandbox
        // callback will auto-verify (see PaymentsController.SandboxCallback).
        var authority = $"{SandboxPrefix}-{Guid.NewGuid():N}";
        return Task.FromResult(new GatewayCreateResult
        {
            Success = true,
            Authority = authority,
            Raw = JsonSerializer.Serialize(new { stub = true, gateway = DisplayName, note = "Sandbox stub — plug real SOAP envelope when terminal credentials are ready", request = r.OrderNumber })
        });
    }

    public virtual Task<GatewayVerifyResult> VerifyAsync(GatewayVerifyInput input, PaymentGatewayConfig cfg, CancellationToken ct = default)
    {
        if (input.Authority.StartsWith(SandboxPrefix + "-", StringComparison.Ordinal))
            return Task.FromResult(new GatewayVerifyResult { Success = true, RefId = input.Authority[^12..], Raw = "{\"stub\":true}" });
        return Task.FromResult(new GatewayVerifyResult { Success = false, Error = $"{DisplayName}: live SOAP verify not configured — set Sandbox=true or complete the integration (see BankSoapGateways.cs header comment)." });
    }
}

public class MellatGateway : BankSoapGatewayBase
{
    public override PaymentGatewayType Type => PaymentGatewayType.Mellat;
    public override string DisplayName => "Bank Mellat (بانک ملت)";
    protected override string SandboxPrefix => "MELLAT";
}

public class SamanGateway : BankSoapGatewayBase
{
    public override PaymentGatewayType Type => PaymentGatewayType.Saman;
    public override string DisplayName => "Bank Saman (بانک سامان)";
    protected override string SandboxPrefix => "SAMAN";
}

public class ParsianGateway : BankSoapGatewayBase
{
    public override PaymentGatewayType Type => PaymentGatewayType.Parsian;
    public override string DisplayName => "Bank Parsian (بانک پارسیان)";
    protected override string SandboxPrefix => "PARSIAN";
}

public class PasargadGateway : BankSoapGatewayBase
{
    public override PaymentGatewayType Type => PaymentGatewayType.Pasargad;
    public override string DisplayName => "Bank Pasargad (بانک پاسارگاد)";
    protected override string SandboxPrefix => "PASARGAD";
}

public class NovinGateway : BankSoapGatewayBase
{
    public override PaymentGatewayType Type => PaymentGatewayType.Novin;
    public override string DisplayName => "Bank Novin (بانک نوین)";
    protected override string SandboxPrefix => "NOVIN";
}

/// <summary>Resolves the gateway implementation per enum + keeps configs cached.</summary>
public class GatewayFactory
{
    private readonly Dictionary<PaymentGatewayType, IPaymentGateway> _map;
    public GatewayFactory(IEnumerable<IPaymentGateway> gateways) => _map = gateways.ToDictionary(g => g.Type);

    public IPaymentGateway? Resolve(PaymentGatewayType type) => _map.TryGetValue(type, out var g) ? g : null;
    public IEnumerable<IPaymentGateway> All => _map.Values;
}
