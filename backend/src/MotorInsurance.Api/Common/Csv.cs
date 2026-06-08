using System.Globalization;
using System.Text;

namespace MotorInsurance.Api.Common;

/// <summary>
/// Minimal RFC 4180 CSV writer used by the export endpoints. Emits a UTF-8 BOM so Excel on a
/// Thai-locale Windows opens the file in UTF-8 (otherwise it mis-decodes Thai as TIS-620).
/// No external dependency — the data sets here are tabular and small.
/// </summary>
public static class Csv
{
    private static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };

    /// <summary>Builds a CSV byte array from a header row and value rows.</summary>
    public static byte[] Build(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", headers.Select(Escape))).Append("\r\n");
        foreach (var row in rows)
            sb.Append(string.Join(",", row.Select(Escape))).Append("\r\n");

        return Bom.Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    /// <summary>Invariant-culture cell formatters so numbers/dates don't pick up Thai separators.</summary>
    public static string Num(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    public static string Num(decimal? v) => v is null ? "" : Num(v.Value);
    public static string Date(DateOnly? v) => v?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
    public static string Date(DateTime? v) => v?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";

    private static string Escape(string? value)
    {
        var v = value ?? "";
        if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }
}
