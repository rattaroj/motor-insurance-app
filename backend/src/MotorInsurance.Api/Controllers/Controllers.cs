using MediatR;
using Microsoft.AspNetCore.Mvc;
using MotorInsurance.Application.Claims.Commands;
using MotorInsurance.Application.Claims.Queries;
using MotorInsurance.Application.Customers.Commands;
using MotorInsurance.Application.Customers.Queries;
using MotorInsurance.Application.Dashboard.Queries;
using MotorInsurance.Application.Payments.Commands;
using MotorInsurance.Application.Payments.Queries;
using MotorInsurance.Application.Policies.Commands;
using MotorInsurance.Application.Policies.Queries;
using MotorInsurance.Application.Quotations.Commands;
using MotorInsurance.Application.Quotations.Queries;
using MotorInsurance.Application.Renewals.Commands;
using MotorInsurance.Application.Vehicles.Commands;
using MotorInsurance.Application.Vehicles.Queries;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}

// ---------- Customers ----------
public record CreateCustomerRequest(string NationalId, string FullName, string? Phone, string? Email);

public class CustomersController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetCustomersQuery(page, pageSize, search), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest r, CancellationToken ct)
    {
        var id = await Mediator.Send(new CreateCustomerCommand(r.NationalId, r.FullName, r.Phone, r.Email), ct);
        return Created($"/api/customers/{id}", new { id });
    }
}

// ---------- Vehicles ----------
public record CreateVehicleRequest(
    long CustomerId, string RegistrationNo, string Province, long ModelYearId, string? ChassisNo);

public class VehiclesController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] long? customerId = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetVehiclesQuery(page, pageSize, search, customerId), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVehicleRequest r, CancellationToken ct)
    {
        var id = await Mediator.Send(
            new CreateVehicleCommand(r.CustomerId, r.RegistrationNo, r.Province, r.ModelYearId, r.ChassisNo), ct);
        return Created($"/api/vehicles/{id}", new { id });
    }
}

// ---------- Vehicle master-data lookups + CRUD (cascading) ----------
public record NameRequest(string Name);
public record CreateModelRequest(long BrandId, string Name);
public record CreateSubmodelRequest(long ModelId, string Name);
public record CreateModelYearRequest(long SubmodelId, int Year);
public record YearRequest(int Year);

public class LookupsController : ApiControllerBase
{
    // ----- Brands -----
    [HttpGet("vehicle-brands")]
    public async Task<IActionResult> Brands(CancellationToken ct)
        => Ok(await Mediator.Send(new GetVehicleBrandsQuery(), ct));

    [HttpPost("vehicle-brands")]
    public async Task<IActionResult> CreateBrand([FromBody] NameRequest r, CancellationToken ct)
        => Ok(new { id = await Mediator.Send(new CreateBrandCommand(r.Name), ct) });

    [HttpPut("vehicle-brands/{id:long}")]
    public async Task<IActionResult> UpdateBrand(long id, [FromBody] NameRequest r, CancellationToken ct)
    {
        await Mediator.Send(new UpdateBrandCommand(id, r.Name), ct);
        return NoContent();
    }

    [HttpDelete("vehicle-brands/{id:long}")]
    public async Task<IActionResult> DeleteBrand(long id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteBrandCommand(id), ct);
        return NoContent();
    }

    // ----- Models -----
    [HttpGet("vehicle-models")]
    public async Task<IActionResult> Models([FromQuery] long brandId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetVehicleModelsQuery(brandId), ct));

    [HttpPost("vehicle-models")]
    public async Task<IActionResult> CreateModel([FromBody] CreateModelRequest r, CancellationToken ct)
        => Ok(new { id = await Mediator.Send(new CreateModelCommand(r.BrandId, r.Name), ct) });

    [HttpPut("vehicle-models/{id:long}")]
    public async Task<IActionResult> UpdateModel(long id, [FromBody] NameRequest r, CancellationToken ct)
    {
        await Mediator.Send(new UpdateModelCommand(id, r.Name), ct);
        return NoContent();
    }

    [HttpDelete("vehicle-models/{id:long}")]
    public async Task<IActionResult> DeleteModel(long id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteModelCommand(id), ct);
        return NoContent();
    }

    // ----- Submodels -----
    [HttpGet("vehicle-submodels")]
    public async Task<IActionResult> Submodels([FromQuery] long modelId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetVehicleSubmodelsQuery(modelId), ct));

    [HttpPost("vehicle-submodels")]
    public async Task<IActionResult> CreateSubmodel([FromBody] CreateSubmodelRequest r, CancellationToken ct)
        => Ok(new { id = await Mediator.Send(new CreateSubmodelCommand(r.ModelId, r.Name), ct) });

    [HttpPut("vehicle-submodels/{id:long}")]
    public async Task<IActionResult> UpdateSubmodel(long id, [FromBody] NameRequest r, CancellationToken ct)
    {
        await Mediator.Send(new UpdateSubmodelCommand(id, r.Name), ct);
        return NoContent();
    }

    [HttpDelete("vehicle-submodels/{id:long}")]
    public async Task<IActionResult> DeleteSubmodel(long id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteSubmodelCommand(id), ct);
        return NoContent();
    }

    // ----- Model years -----
    [HttpGet("vehicle-model-years")]
    public async Task<IActionResult> ModelYears([FromQuery] long submodelId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetVehicleModelYearsQuery(submodelId), ct));

    [HttpPost("vehicle-model-years")]
    public async Task<IActionResult> CreateModelYear([FromBody] CreateModelYearRequest r, CancellationToken ct)
        => Ok(new { id = await Mediator.Send(new CreateModelYearCommand(r.SubmodelId, r.Year), ct) });

    [HttpPut("vehicle-model-years/{id:long}")]
    public async Task<IActionResult> UpdateModelYear(long id, [FromBody] YearRequest r, CancellationToken ct)
    {
        await Mediator.Send(new UpdateModelYearCommand(id, r.Year), ct);
        return NoContent();
    }

    [HttpDelete("vehicle-model-years/{id:long}")]
    public async Task<IActionResult> DeleteModelYear(long id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteModelYearCommand(id), ct);
        return NoContent();
    }
}

// ---------- Quotations ----------
public record CreateQuotationRequest(long CustomerId, long VehicleId, CoverageType CoverageType, decimal SumInsured);

public class QuotationsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetQuotationsQuery(page, pageSize, search), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuotationRequest r, CancellationToken ct)
    {
        var id = await Mediator.Send(new CreateQuotationCommand(r.CustomerId, r.VehicleId, r.CoverageType, r.SumInsured), ct);
        return CreatedAtAction(null, new { id }, new { id });
    }
}

// ---------- Policies ----------
public record IssuePolicyRequest(long QuotationId, DateOnly EffectiveDate);
public record CancelPolicyRequest(string Reason);

public class PoliciesController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetPoliciesQuery(page, pageSize, status, search), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Ok(await Mediator.Send(new GetPolicyByIdQuery(id), ct));

    [HttpGet("{id:long}/history")]
    public async Task<IActionResult> History(long id, CancellationToken ct)
        => Ok(await Mediator.Send(new GetPolicyHistoryQuery(id), ct));

    [HttpPost("issue")]
    public async Task<IActionResult> Issue([FromBody] IssuePolicyRequest r, CancellationToken ct)
    {
        var id = await Mediator.Send(new IssuePolicyCommand(r.QuotationId, r.EffectiveDate), ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPost("{id:long}/activate")]
    public async Task<IActionResult> Activate(long id, CancellationToken ct)
    {
        await Mediator.Send(new ActivatePolicyCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, [FromBody] CancelPolicyRequest r, CancellationToken ct)
    {
        await Mediator.Send(new CancelPolicyCommand(id, r.Reason), ct);
        return NoContent();
    }
}

// ---------- Renewals ----------
public record RenewRequest(decimal? AdjustedSumInsured);

public class RenewalsController : ApiControllerBase
{
    [HttpPost("{policyId:long}")]
    public async Task<IActionResult> Renew(long policyId, [FromBody] RenewRequest r, CancellationToken ct)
    {
        var id = await Mediator.Send(new RenewPolicyCommand(policyId, r.AdjustedSumInsured), ct);
        return Created($"/api/policies/{id}", new { id });
    }
}

// ---------- Claims ----------
public record FileClaimRequest(long PolicyId, DateOnly IncidentDate, string? Description, decimal ClaimedAmount);
public record AdvanceClaimRequest(ClaimStatus To);
public record ApproveClaimRequest(decimal ApprovedAmount);
public record RejectClaimRequest(string Reason);

public class ClaimsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        [FromQuery] long? policyId = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetClaimsQuery(page, pageSize, search, status, policyId), ct));

    [HttpPost]
    public async Task<IActionResult> File([FromBody] FileClaimRequest r, CancellationToken ct)
    {
        var id = await Mediator.Send(new FileClaimCommand(r.PolicyId, r.IncidentDate, r.Description, r.ClaimedAmount), ct);
        return Created($"/api/claims/{id}", new { id });
    }

    [HttpPost("{id:long}/advance")]
    public async Task<IActionResult> Advance(long id, [FromBody] AdvanceClaimRequest r, CancellationToken ct)
    {
        await Mediator.Send(new AdvanceClaimCommand(id, r.To), ct);
        return NoContent();
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] ApproveClaimRequest r, CancellationToken ct)
    {
        await Mediator.Send(new ApproveClaimCommand(id, r.ApprovedAmount), ct);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] RejectClaimRequest r, CancellationToken ct)
    {
        await Mediator.Send(new RejectClaimCommand(id, r.Reason), ct);
        return NoContent();
    }
}

// ---------- Payments ----------
public record SettlePaymentRequest(string ReferenceNo);

public class PaymentsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] string? direction = null,
        [FromQuery] long? policyId = null, [FromQuery] long? claimId = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetPaymentsQuery(page, pageSize, search, status, direction, policyId, claimId), ct));

    [HttpPost("{id:long}/settle")]
    public async Task<IActionResult> Settle(long id, [FromBody] SettlePaymentRequest r, CancellationToken ct)
    {
        await Mediator.Send(new SettlePaymentCommand(id, r.ReferenceNo), ct);
        return NoContent();
    }
}

// ---------- Dashboard ----------
public class DashboardController : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
        => Ok(await Mediator.Send(new GetDashboardSummaryQuery(), ct));
}
