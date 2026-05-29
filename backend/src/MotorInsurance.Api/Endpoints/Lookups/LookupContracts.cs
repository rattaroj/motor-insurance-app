using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Api.Endpoints.Lookups;

// Shared read/option DTOs + the create response for the vehicle master-data lookups.
public record OptionDto(long Id, string Name);
public record SubmodelOptionDto(long Id, string Name, Powertrain Powertrain);
public record ModelYearOptionDto(long Id, int Year);
public record IdResponse(long Id);
