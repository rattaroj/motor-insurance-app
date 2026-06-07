using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Api.Documents;

/// <summary>Thai display labels for enums on printed documents (mirrors the frontend labels).</summary>
public static class ThaiLabels
{
    public static string Coverage(CoverageType c) => c switch
    {
        CoverageType.Type1 => "ประกันชั้น 1",
        CoverageType.Type2Plus => "ประกันชั้น 2+",
        CoverageType.Type3Plus => "ประกันชั้น 3+",
        CoverageType.Type3 => "ประกันชั้น 3",
        _ => c.ToString(),
    };

    public static string ClaimStatus(ClaimStatus s) => s switch
    {
        Domain.Enums.ClaimStatus.Filed => "แจ้งเคลม",
        Domain.Enums.ClaimStatus.UnderReview => "กำลังตรวจสอบ",
        Domain.Enums.ClaimStatus.Assessment => "ประเมินความเสียหาย",
        Domain.Enums.ClaimStatus.Approved => "อนุมัติ",
        Domain.Enums.ClaimStatus.Rejected => "ปฏิเสธ",
        Domain.Enums.ClaimStatus.Paid => "จ่ายสินไหมแล้ว",
        Domain.Enums.ClaimStatus.Closed => "ปิดเคลม",
        _ => s.ToString(),
    };

    public static string PolicyStatus(PolicyStatus s) => s switch
    {
        Domain.Enums.PolicyStatus.Draft => "ฉบับร่าง",
        Domain.Enums.PolicyStatus.Quoted => "เสนอราคาแล้ว",
        Domain.Enums.PolicyStatus.Issued => "ออกกรมธรรม์แล้ว",
        Domain.Enums.PolicyStatus.Active => "คุ้มครองอยู่",
        Domain.Enums.PolicyStatus.Cancelled => "ยกเลิก",
        Domain.Enums.PolicyStatus.Expired => "หมดอายุ",
        _ => s.ToString(),
    };
}
