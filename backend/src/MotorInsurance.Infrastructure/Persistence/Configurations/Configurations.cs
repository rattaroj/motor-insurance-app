using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customer");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.NationalId).HasColumnName("national_id").HasMaxLength(13).IsRequired();
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(20);
        b.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        b.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        b.Property(x => x.BirthDate).HasColumnName("birth_date");
        b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
        b.Property(x => x.LineUserId).HasColumnName("line_user_id").HasMaxLength(64);
        b.Property(x => x.AddressLine).HasColumnName("address_line").HasMaxLength(255);
        b.Property(x => x.ProvinceId).HasColumnName("province_id");
        b.Property(x => x.DistrictId).HasColumnName("district_id");
        b.Property(x => x.SubdistrictId).HasColumnName("subdistrict_id");
        b.Property(x => x.PostalCodeId).HasColumnName("postal_code_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => x.NationalId).IsUnique();
        b.HasOne(x => x.Province).WithMany().HasForeignKey(x => x.ProvinceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.District).WithMany().HasForeignKey(x => x.DistrictId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Subdistrict).WithMany().HasForeignKey(x => x.SubdistrictId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PostalCode).WithMany().HasForeignKey(x => x.PostalCodeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.ToTable("vehicle");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.RegistrationNo).HasColumnName("registration_no").HasMaxLength(20).IsRequired();
        b.Property(x => x.Province).HasColumnName("province").HasMaxLength(50).IsRequired();
        b.Property(x => x.ModelYearId).HasColumnName("model_year_id");
        b.Property(x => x.ChassisNo).HasColumnName("chassis_no").HasMaxLength(50);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Customer).WithMany(c => c.Vehicles)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ModelYear).WithMany()
            .HasForeignKey(x => x.ModelYearId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerTitleConfiguration : IEntityTypeConfiguration<CustomerTitle>
{
    public void Configure(EntityTypeBuilder<CustomerTitle> b)
    {
        b.ToTable("customer_title");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public class VehicleBrandConfiguration : IEntityTypeConfiguration<VehicleBrand>
{
    public void Configure(EntityTypeBuilder<VehicleBrand> b)
    {
        b.ToTable("vehicle_brand");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public class VehicleModelConfiguration : IEntityTypeConfiguration<VehicleModel>
{
    public void Configure(EntityTypeBuilder<VehicleModel> b)
    {
        b.ToTable("vehicle_model");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.BrandId).HasColumnName("brand_id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        b.HasIndex(x => new { x.BrandId, x.Name }).IsUnique();
        b.HasOne(x => x.Brand).WithMany(br => br.Models)
            .HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class VehicleSubmodelConfiguration : IEntityTypeConfiguration<VehicleSubmodel>
{
    public void Configure(EntityTypeBuilder<VehicleSubmodel> b)
    {
        b.ToTable("vehicle_submodel");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ModelId).HasColumnName("model_id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        b.Property(x => x.Powertrain).HasColumnName("powertrain");
        PowertrainConverter.Apply(b.Property(x => x.Powertrain));
        b.HasIndex(x => new { x.ModelId, x.Name }).IsUnique();
        b.HasOne(x => x.Model).WithMany(m => m.Submodels)
            .HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class VehicleModelYearConfiguration : IEntityTypeConfiguration<VehicleModelYear>
{
    public void Configure(EntityTypeBuilder<VehicleModelYear> b)
    {
        b.ToTable("vehicle_model_year");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.SubmodelId).HasColumnName("submodel_id");
        b.Property(x => x.Year).HasColumnName("year");
        b.HasIndex(x => new { x.SubmodelId, x.Year }).IsUnique();
        b.HasOne(x => x.Submodel).WithMany(s => s.ModelYears)
            .HasForeignKey(x => x.SubmodelId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> b)
    {
        b.ToTable("province");
        b.HasKey(x => x.Id);
        // Id is the official province code (supplied, not DB-generated).
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.NameTh).HasColumnName("name_th").HasMaxLength(150).IsRequired();
        b.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(150).IsRequired();
    }
}

public class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> b)
    {
        b.ToTable("district");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.ProvinceId).HasColumnName("province_id");
        b.Property(x => x.NameTh).HasColumnName("name_th").HasMaxLength(150).IsRequired();
        b.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.ProvinceId);
        b.HasOne(x => x.Province).WithMany(p => p.Districts)
            .HasForeignKey(x => x.ProvinceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PostalCodeConfiguration : IEntityTypeConfiguration<PostalCode>
{
    public void Configure(EntityTypeBuilder<PostalCode> b)
    {
        b.ToTable("postal_code");
        b.HasKey(x => x.Id);
        // Id is the 5-digit postal code as a number (supplied, not DB-generated).
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(5).IsFixedLength().IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class SubdistrictConfiguration : IEntityTypeConfiguration<Subdistrict>
{
    public void Configure(EntityTypeBuilder<Subdistrict> b)
    {
        b.ToTable("subdistrict");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.DistrictId).HasColumnName("district_id");
        b.Property(x => x.PostalCodeId).HasColumnName("postal_code_id");
        b.Property(x => x.NameTh).HasColumnName("name_th").HasMaxLength(150).IsRequired();
        b.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.DistrictId);
        b.HasIndex(x => x.PostalCodeId);
        b.HasOne(x => x.District).WithMany(d => d.Subdistricts)
            .HasForeignKey(x => x.DistrictId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PostalCode).WithMany(p => p.Subdistricts)
            .HasForeignKey(x => x.PostalCodeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal static class CoverageConverter
{
    // Switch expressions can't appear inside an expression tree, so the conversion
    // logic lives in static methods that the converter expressions merely call.
    public static void Apply(PropertyBuilder<CoverageType> b) =>
        b.HasConversion(v => ToDb(v), v => FromDb(v)).HasMaxLength(20);

    private static string ToDb(CoverageType v) => v switch
    {
        CoverageType.Type1 => "TYPE1",
        CoverageType.Type2Plus => "TYPE2PLUS",
        CoverageType.Type3Plus => "TYPE3PLUS",
        CoverageType.Type3 => "TYPE3",
        _ => "TYPE1"
    };

    private static CoverageType FromDb(string v) => v switch
    {
        "TYPE1" => CoverageType.Type1,
        "TYPE2PLUS" => CoverageType.Type2Plus,
        "TYPE3PLUS" => CoverageType.Type3Plus,
        "TYPE3" => CoverageType.Type3,
        _ => CoverageType.Type1
    };
}

internal static class PowertrainConverter
{
    public static void Apply(PropertyBuilder<Powertrain> b) =>
        b.HasConversion(v => ToDb(v), v => FromDb(v)).HasMaxLength(20);

    private static string ToDb(Powertrain v) => v switch
    {
        Powertrain.Gasoline => "GASOLINE",
        Powertrain.Diesel => "DIESEL",
        Powertrain.Electric => "ELECTRIC",
        Powertrain.Hybrid => "HYBRID",
        _ => "GASOLINE"
    };

    private static Powertrain FromDb(string v) => v switch
    {
        "GASOLINE" => Powertrain.Gasoline,
        "DIESEL" => Powertrain.Diesel,
        "ELECTRIC" => Powertrain.Electric,
        "HYBRID" => Powertrain.Hybrid,
        _ => Powertrain.Gasoline
    };
}

public class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> b)
    {
        b.ToTable("quotation");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.QuotationNo).HasColumnName("quotation_no").HasMaxLength(30).IsRequired();
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.VehicleId).HasColumnName("vehicle_id");
        b.Property(x => x.CoverageType).HasColumnName("coverage_type");
        CoverageConverter.Apply(b.Property(x => x.CoverageType));
        b.Property(x => x.SumInsured).HasColumnName("sum_insured").HasColumnType("decimal(18,2)");
        b.Property(x => x.Premium).HasColumnName("premium").HasColumnType("decimal(18,2)");
        b.Property(x => x.BasePremium).HasColumnName("base_premium").HasColumnType("decimal(18,2)");
        b.Property(x => x.NcbPercent).HasColumnName("ncb_percent");
        b.Property(x => x.Deductible).HasColumnName("deductible").HasColumnType("decimal(18,2)");
        b.Property(x => x.ValidUntil).HasColumnName("valid_until");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => x.QuotationNo).IsUnique();
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RiderConfiguration : IEntityTypeConfiguration<Rider>
{
    public void Configure(EntityTypeBuilder<Rider> b)
    {
        b.ToTable("rider");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        b.Property(x => x.Premium).HasColumnName("premium").HasColumnType("decimal(18,2)");
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public class PremiumRateConfiguration : IEntityTypeConfiguration<PremiumRate>
{
    public void Configure(EntityTypeBuilder<PremiumRate> b)
    {
        b.ToTable("premium_rate");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Coverage).HasColumnName("coverage_type");
        CoverageConverter.Apply(b.Property(x => x.Coverage));
        b.Property(x => x.Rate).HasColumnName("rate").HasColumnType("decimal(6,4)");
        b.Property(x => x.EffectiveDate).HasColumnName("effective_date");
        // One rate per coverage type per effective date (effective-dated versioning).
        b.HasIndex(x => new { x.Coverage, x.EffectiveDate }).IsUnique();
    }
}

public class AgeLoadingBandConfiguration : IEntityTypeConfiguration<AgeLoadingBand>
{
    public void Configure(EntityTypeBuilder<AgeLoadingBand> b)
    {
        b.ToTable("age_loading_band");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.MaxAge).HasColumnName("max_age");
        b.Property(x => x.Surcharge).HasColumnName("surcharge").HasColumnType("decimal(6,4)");
        b.Property(x => x.EffectiveDate).HasColumnName("effective_date");
        b.HasIndex(x => x.EffectiveDate);
    }
}

public class RatingSettingConfiguration : IEntityTypeConfiguration<RatingSetting>
{
    public void Configure(EntityTypeBuilder<RatingSetting> b)
    {
        b.ToTable("rating_setting");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(50);
        b.Property(x => x.Value).HasColumnName("value").HasColumnType("decimal(9,4)");
    }
}

public class QuotationRiderConfiguration : IEntityTypeConfiguration<QuotationRider>
{
    public void Configure(EntityTypeBuilder<QuotationRider> b)
    {
        b.ToTable("quotation_rider");
        b.HasKey(x => new { x.QuotationId, x.RiderId });
        b.Property(x => x.QuotationId).HasColumnName("quotation_id");
        b.Property(x => x.RiderId).HasColumnName("rider_id");
        b.HasOne(x => x.Quotation).WithMany(q => q.Riders)
            .HasForeignKey(x => x.QuotationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Rider).WithMany()
            .HasForeignKey(x => x.RiderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PolicyRiderConfiguration : IEntityTypeConfiguration<PolicyRider>
{
    public void Configure(EntityTypeBuilder<PolicyRider> b)
    {
        b.ToTable("policy_rider");
        b.HasKey(x => new { x.PolicyId, x.RiderId });
        b.Property(x => x.PolicyId).HasColumnName("policy_id");
        b.Property(x => x.RiderId).HasColumnName("rider_id");
        b.HasOne(x => x.Policy).WithMany(p => p.Riders)
            .HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Rider).WithMany()
            .HasForeignKey(x => x.RiderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QuotationDriverConfiguration : IEntityTypeConfiguration<QuotationDriver>
{
    public void Configure(EntityTypeBuilder<QuotationDriver> b)
    {
        b.ToTable("quotation_driver");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.QuotationId).HasColumnName("quotation_id");
        b.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        b.Property(x => x.NationalId).HasColumnName("national_id").HasMaxLength(13).IsRequired();
        b.Property(x => x.IdCardImagePath).HasColumnName("id_card_image_path").HasMaxLength(400).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Quotation).WithMany(q => q.Drivers)
            .HasForeignKey(x => x.QuotationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EndorsementConfiguration : IEntityTypeConfiguration<Endorsement>
{
    public void Configure(EntityTypeBuilder<Endorsement> b)
    {
        b.ToTable("endorsement");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.EndorsementNo).HasColumnName("endorsement_no").HasMaxLength(30).IsRequired();
        b.Property(x => x.PolicyId).HasColumnName("policy_id");
        b.Property(x => x.FieldName).HasColumnName("field_name").HasMaxLength(50).IsRequired();
        b.Property(x => x.OldValue).HasColumnName("old_value").HasMaxLength(500);
        b.Property(x => x.NewValue).HasColumnName("new_value").HasMaxLength(500);
        b.Property(x => x.EffectiveDate).HasColumnName("effective_date");
        b.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => x.EndorsementNo).IsUnique();
        b.HasOne(x => x.Policy).WithMany(p => p.Endorsements)
            .HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> b)
    {
        b.ToTable("policy", t => t.IsTemporal(tt =>
        {
            tt.HasPeriodStart("ValidFrom").HasColumnName("valid_from");
            tt.HasPeriodEnd("ValidTo").HasColumnName("valid_to");
            tt.UseHistoryTable("policy_history");
        }));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PolicyNo).HasColumnName("policy_no").HasMaxLength(30).IsRequired();
        b.Property(x => x.QuotationId).HasColumnName("quotation_id");
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.VehicleId).HasColumnName("vehicle_id");
        b.Property(x => x.Status).HasColumnName("status_code").HasMaxLength(20)
            .HasConversion<string>();
        b.Property(x => x.CoverageType).HasColumnName("coverage_type");
        CoverageConverter.Apply(b.Property(x => x.CoverageType));
        b.Property(x => x.SumInsured).HasColumnName("sum_insured").HasColumnType("decimal(18,2)");
        b.Property(x => x.Premium).HasColumnName("premium").HasColumnType("decimal(18,2)");
        b.Property(x => x.BasePremium).HasColumnName("base_premium").HasColumnType("decimal(18,2)");
        b.Property(x => x.NcbPercent).HasColumnName("ncb_percent");
        b.Property(x => x.Deductible).HasColumnName("deductible").HasColumnType("decimal(18,2)");
        b.Property(x => x.EffectiveDate).HasColumnName("effective_date");
        b.Property(x => x.ExpiryDate).HasColumnName("expiry_date");
        b.Property(x => x.PreviousPolicyId).HasColumnName("previous_policy_id");
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => x.PolicyNo).IsUnique();
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Quotation).WithMany().HasForeignKey(x => x.QuotationId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.PreviousPolicy).WithMany()
            .HasForeignKey(x => x.PreviousPolicyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> b)
    {
        b.ToTable("claim", t => t.IsTemporal(tt =>
        {
            tt.HasPeriodStart("ValidFrom").HasColumnName("valid_from");
            tt.HasPeriodEnd("ValidTo").HasColumnName("valid_to");
            tt.UseHistoryTable("claim_history");
        }));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ClaimNo).HasColumnName("claim_no").HasMaxLength(30).IsRequired();
        b.Property(x => x.PolicyId).HasColumnName("policy_id");
        b.Property(x => x.Status).HasColumnName("status_code").HasMaxLength(20)
            .HasConversion<string>();
        b.Property(x => x.IncidentDate).HasColumnName("incident_date");
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        b.Property(x => x.ClaimedAmount).HasColumnName("claimed_amount").HasColumnType("decimal(18,2)");
        b.Property(x => x.ApprovedAmount).HasColumnName("approved_amount").HasColumnType("decimal(18,2)");
        b.Property(x => x.RejectReason).HasColumnName("reject_reason").HasMaxLength(500);
        b.Property(x => x.GarageId).HasColumnName("garage_id");
        b.Property(x => x.SurveyorName).HasColumnName("surveyor_name").HasMaxLength(150);
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => x.ClaimNo).IsUnique();
        b.HasOne(x => x.Policy).WithMany(p => p.Claims)
            .HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Garage).WithMany()
            .HasForeignKey(x => x.GarageId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class GarageConfiguration : IEntityTypeConfiguration<Garage>
{
    public void Configure(EntityTypeBuilder<Garage> b)
    {
        b.ToTable("garage");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(30);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public class ClaimPhotoConfiguration : IEntityTypeConfiguration<ClaimPhoto>
{
    public void Configure(EntityTypeBuilder<ClaimPhoto> b)
    {
        b.ToTable("claim_photo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ClaimId).HasColumnName("claim_id");
        b.Property(x => x.ImagePath).HasColumnName("image_path").HasMaxLength(400).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => x.ClaimId);
        b.HasOne(x => x.Claim).WithMany(c => c.Photos)
            .HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payment");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PaymentNo).HasColumnName("payment_no").HasMaxLength(30).IsRequired();
        b.Property(x => x.Direction).HasColumnName("direction_code").HasMaxLength(20)
            .HasConversion(
                v => v == PaymentDirection.Inbound ? "INBOUND" : "OUTBOUND",
                v => v == "INBOUND" ? PaymentDirection.Inbound : PaymentDirection.Outbound);
        b.Property(x => x.Status).HasColumnName("status_code").HasMaxLength(20)
            .HasConversion<string>();
        b.Property(x => x.PolicyId).HasColumnName("policy_id");
        b.Property(x => x.ClaimId).HasColumnName("claim_id");
        b.Property(x => x.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)");
        b.Property(x => x.PaidAt).HasColumnName("paid_at");
        b.Property(x => x.ReferenceNo).HasColumnName("reference_no").HasMaxLength(100);
        b.Property(x => x.InstallmentPlanId).HasColumnName("installment_plan_id");
        b.Property(x => x.InstallmentSeq).HasColumnName("installment_seq");
        b.Property(x => x.DueDate).HasColumnName("due_date");
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => x.PaymentNo).IsUnique();
        b.HasOne(x => x.Policy).WithMany(p => p.Payments)
            .HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Claim).WithMany(c => c.Payments)
            .HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.InstallmentPlan).WithMany(ip => ip.Payments)
            .HasForeignKey(x => x.InstallmentPlanId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InstallmentPlanConfiguration : IEntityTypeConfiguration<InstallmentPlan>
{
    public void Configure(EntityTypeBuilder<InstallmentPlan> b)
    {
        b.ToTable("installment_plan");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PolicyId).HasColumnName("policy_id");
        b.Property(x => x.TotalPremium).HasColumnName("total_premium").HasColumnType("decimal(18,2)");
        b.Property(x => x.Fee).HasColumnName("fee").HasColumnType("decimal(18,2)");
        b.Property(x => x.Installments).HasColumnName("installments");
        b.Property(x => x.FrequencyDays).HasColumnName("frequency_days");
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Policy).WithMany()
            .HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notification");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PolicyId).HasColumnName("policy_id");
        b.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(20).IsRequired();
        b.Property(x => x.Recipient).HasColumnName("recipient").HasMaxLength(200).IsRequired();
        b.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).HasColumnName("body").HasMaxLength(2000).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        b.Property(x => x.SentAt).HasColumnName("sent_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => x.PolicyId);
        b.HasOne(x => x.Policy).WithMany()
            .HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Restrict);
    }
}
