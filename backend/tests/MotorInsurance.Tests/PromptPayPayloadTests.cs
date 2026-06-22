using MotorInsurance.Infrastructure.Services;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>Locks the Thai PromptPay EMVCo payload format (and CRC tail) after the move to Infrastructure.</summary>
public class PromptPayPayloadTests
{
    [Fact]
    public void Builds_a_dynamic_emv_payload_with_amount_and_crc()
    {
        var payload = PromptPayPayload.Build("0812345678", 3000m);

        Assert.StartsWith("000201010212", payload);   // payload format (00) + dynamic QR (01=12)
        Assert.Contains("A000000677010111", payload);  // PromptPay application id
        Assert.Contains("54073000.00", payload);       // amount field: id 54, length 07, "3000.00"
        Assert.Contains("5303764", payload);           // currency THB
        Assert.Contains("5802TH", payload);            // country
        Assert.Matches("6304[0-9A-F]{4}$", payload);   // CRC tag + 4 upper-hex chars at the very end
    }

    [Fact]
    public void Encodes_a_mobile_number_as_0066_prefixed()
    {
        var payload = PromptPayPayload.Build("0812345678", 100m);
        Assert.Contains("0066812345678", payload);     // leading 0 dropped, 0066 + 9 digits
    }
}
