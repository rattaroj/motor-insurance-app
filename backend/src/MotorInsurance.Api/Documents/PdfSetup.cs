using System.Globalization;
using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace MotorInsurance.Api.Documents;

/// <summary>
/// One-time QuestPDF configuration: Community license + the embedded Thai font (Sarabun, OFL),
/// so Thai glyphs render instead of tofu (QuestPDF only bundles Lato by default).
/// </summary>
public static class PdfSetup
{
    /// <summary>Font family used by all document templates.</summary>
    public const string FontFamily = "Sarabun";

    /// <summary>Thai culture (Buddhist-era years) for deterministic date formatting, independent of
    /// the host's thread culture — matches the frontend which displays พ.ศ. years.</summary>
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("th-TH");

    public static void Configure()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        RegisterEmbeddedFont("Sarabun-Regular.ttf");
        RegisterEmbeddedFont("Sarabun-Bold.ttf");
    }

    private static void RegisterEmbeddedFont(string logicalName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded font '{logicalName}' not found.");
        FontManager.RegisterFont(stream);
    }
}
