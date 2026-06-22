using MotorInsurance.Application.Common.Interfaces;

namespace MotorInsurance.Application.Payments;

/// <summary>
/// Builds the scan-to-pay PromptPay QR for an installment reminder: a PNG attachment plus a short
/// body line, but only when a generator is supplied <em>and</em> the reminder goes out by email (the
/// only channel that carries attachments). Returns empties otherwise, so call sites stay uniform.
/// </summary>
public static class PromptPayReminderAttachment
{
    public static (byte[]? Bytes, string? Name, string? ContentType, string BodyLine) For(
        IPromptPayQrGenerator? qr, string channel, string policyNo, decimal amount)
    {
        if (qr is null || !string.Equals(channel, "Email", StringComparison.OrdinalIgnoreCase))
            return (null, null, null, "");

        return (qr.CreatePng(amount), $"promptpay-{policyNo}.png", "image/png",
            "\nสแกน QR พร้อมเพย์ที่แนบมาในอีเมลนี้เพื่อชำระได้ทันที");
    }
}
