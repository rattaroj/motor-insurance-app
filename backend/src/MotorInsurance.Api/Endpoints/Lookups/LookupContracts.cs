using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Api.Endpoints.Lookups;

// Shared read/option DTOs + the create response for the vehicle master-data lookups.
public record OptionDto(long Id, string Name);
public record SubmodelOptionDto(long Id, string Name, Powertrain Powertrain);
public record ModelYearOptionDto(long Id, int Year);
public record IdResponse(long Id);

// Thai administrative-division lookups carry bilingual names (Thai for display, English fallback).
public record GeoOptionDto(long Id, string NameTh, string NameEn);
public record SubdistrictOptionDto(long Id, string NameTh, string NameEn, long PostalCodeId, string PostalCode);
public record PostalCodeOptionDto(long Id, string Code);

// Add-on rider option carries its flat premium (used by the quotation form + master CRUD).
public record RiderDto(long Id, string Name, decimal Premium);
