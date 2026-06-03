using System.Globalization;
using System.Text;

namespace MotorInsurance.Api.Endpoints.Payments;

/// <summary>
/// Builds a Thai PromptPay EMVCo QR payload (dynamic, amount-specific) per the BOT spec.
/// Target is a mobile number (10 digits) or a national/tax id (13 digits).
/// </summary>
public static class PromptPayPayload
{
    public static string Build(string target, decimal amount)
    {
        var sb = new StringBuilder();
        sb.Append(Field("00", "01"));                 // payload format indicator
        sb.Append(Field("01", "12"));                 // dynamic QR (amount present)
        sb.Append(Field("29", Field("00", "A000000677010111") + AccountField(target)));
        sb.Append(Field("53", "764"));                // currency THB
        sb.Append(Field("54", amount.ToString("0.00", CultureInfo.InvariantCulture)));
        sb.Append(Field("58", "TH"));                 // country
        sb.Append("6304");                            // CRC tag + length, value appended below
        sb.Append(Crc16(sb.ToString()));
        return sb.ToString();
    }

    private static string AccountField(string target)
    {
        var digits = new string(target.Where(char.IsDigit).ToArray());
        return digits.Length == 13
            ? Field("02", digits)                                  // national / tax id
            : Field("01", "0066" + digits.TrimStart('0'));         // mobile → 0066 + 9 digits
    }

    private static string Field(string id, string value) => id + value.Length.ToString("D2") + value;

    /// <summary>CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF), 4 upper-hex chars.</summary>
    private static string Crc16(string input)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(input))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }
        return crc.ToString("X4");
    }
}
