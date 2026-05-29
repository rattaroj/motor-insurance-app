using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Auth.Commands;
using MotorInsurance.Application.Auth.Queries;
using MotorInsurance.Application.Common.Authorization;
using MotorInsurance.Application.Customers.Commands;
using MotorInsurance.Application.Customers.Queries;
using MotorInsurance.Application.Dashboard.Queries;
using MotorInsurance.Application.Payments.Commands;
using MotorInsurance.Application.Payments.Queries;
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

// ---------- Auth ----------
public record LoginRequest(string Username, string Password);
public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserProfileDto User);

[Route("api/[controller]")]
public class AuthController : ApiControllerBase
{
    private const string RefreshCookie = "refresh_token";
    private const string RefreshCookiePath = "/api/auth";

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest r, CancellationToken ct)
    {
        var result = await Mediator.Send(new LoginCommand(r.Username, r.Password), ct);
        SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(new AuthResponse(result.AccessToken, result.ExpiresAt, result.User));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var raw = Request.Cookies[RefreshCookie];
        var result = await Mediator.Send(new RefreshTokenCommand(raw), ct);
        SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(new AuthResponse(result.AccessToken, result.ExpiresAt, result.User));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await Mediator.Send(new LogoutCommand(Request.Cookies[RefreshCookie]), ct);
        Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = RefreshCookiePath });
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
        => Ok(await Mediator.Send(new GetMeQuery(), ct));

    private void SetRefreshCookie(string rawToken, DateTime expiresAtUtc) =>
        Response.Cookies.Append(RefreshCookie, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps, // dev runs over http://localhost; enable Secure under https
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookiePath,
            Expires = expiresAtUtc,
        });
}

// ---------- Customers ----------
public record CreateCustomerRequest(string NationalId, string FullName, string? Phone, string? Email);

public class CustomersController : ApiControllerBase
{
    [RequirePermission(Permissions.CustomerRead)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetCustomersQuery(page, pageSize, search), ct));

    [RequirePermission(Permissions.CustomerWrite)]
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
    [RequirePermission(Permissions.VehicleRead)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] long? customerId = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetVehiclesQuery(page, pageSize, search, customerId), ct));

    [RequirePermission(Permissions.VehicleWrite)]
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
public record CreateSubmodelRequest(long ModelId, string Name, Powertrain Powertrain);
public record SubmodelRequest(string Name, Powertrain Powertrain);
public record CreateModelYearRequest(long SubmodelId, int Year);
public record YearRequest(int Year);

public class LookupsController : ApiControllerBase
{
    // ----- Brands -----
    [RequirePermission(Permissions.LookupRead)]
    [HttpGet("vehicle-brands")]
    public async Task<IActionResult> Brands(CancellationToken ct)
        => Ok(await Mediator.Send(new GetVehicleBrandsQuery(), ct));

    [RequirePermission(Permissions.LookupManage)]
    [HttpPost("vehicle-brands")]
    public async Task<IActionResult> CreateBrand([FromBody] NameRequest r, CancellationToken ct)
        => Ok(new { id = await Mediator.Send(new CreateBrandCommand(r.Name), ct) });

    [RequirePermission(Permissions.LookupManage)]
    [HttpPut("vehicle-brands/{id:long}")]
    public async Task<IActionResult> UpdateBrand(long id, [FromBody] NameRequest r, CancellationToken ct)
    {
        await Mediator.Send(new UpdateBrandCommand(id, r.Name), ct);
        return NoContent();
    }

    [RequirePermission(Permissions.LookupManage)]
    [HttpDelete("vehicle-brands/{id:long}")]
    public async Task<IActionResult> DeleteBrand(long id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteBrandCommand(id), ct);
        return NoContent();
    }

    // ----- Models -----
    [RequirePermission(Permissions.LookupRead)]
    [HttpGet("vehicle-models")]
    public async Task<IActionResult> Models([FromQuery] long brandId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetVehicleModelsQuery(brandId), ct));

    [RequirePermission(Permissions.LookupManage)]
    [HttpPost("vehicle-models")]
    public async Task<IActionResult> CreateModel([FromBody] CreateModelRequest r, CancellationToken ct)
        => Ok(new { id = await Mediator.Send(new CreateModelCommand(r.BrandId, r.Name), ct) });

    [RequirePermission(Permissions.LookupManage)]
    [HttpPut("vehicle-models/{id:long}")]
    public async Task<IActionResult> UpdateModel(long id, [FromBody] NameRequest r, CancellationToken ct)
    {
        await Mediator.Send(new UpdateModelCommand(id, r.Name), ct);
        return NoContent();
    }

    [RequirePermission(Permissions.LookupManage)]
    [HttpDelete("vehicle-models/{id:long}")]
    public async Task<IActionResult> DeleteModel(long id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteModelCommand(id), ct);
        return NoContent();
    }

    // ----- Submodels -----
    [RequirePermission(Permissions.LookupRead)]
    [HttpGet("vehicle-submodels")]
    public async Task<IActionResult> Submodels([FromQuery] long modelId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetVehicleSubmodelsQuery(modelId), ct));

    [RequirePermission(Permissions.LookupManage)]
    [HttpPost("vehicle-submodels")]
    public async Task<IActionResult> CreateSubmodel([FromBody] CreateSubmodelRequest r, CancellationToken ct)
        => Ok(new { id = await Mediator.Send(new CreateSubmodelCommand(r.ModelId, r.Name, r.Powertrain), ct) });

    [RequirePermission(Permissions.LookupManage)]
    [HttpPut("vehicle-submodels/{id:long}")]
    public async Task<IActionResult> UpdateSubmodel(long id, [FromBody] SubmodelRequest r, CancellationToken ct)
    {
        await Mediator.Send(new UpdateSubmodelCommand(id, r.Name, r.Powertrain), ct);
        return NoContent();
    }

    [RequirePermission(Permissions.LookupManage)]
    [HttpDelete("vehicle-submodels/{id:long}")]
    public async Task<IActionResult> DeleteSubmodel(long id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteSubmodelCommand(id), ct);
        return NoContent();
    }

    // ----- Model years -----
    [RequirePermission(Permissions.LookupRead)]
    [HttpGet("vehicle-model-years")]
    public async Task<IActionResult> ModelYears([FromQuery] long submodelId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetVehicleModelYearsQuery(submodelId), ct));

    [RequirePermission(Permissions.LookupManage)]
    [HttpPost("vehicle-model-years")]
    public async Task<IActionResult> CreateModelYear([FromBody] CreateModelYearRequest r, CancellationToken ct)
        => Ok(new { id = await Mediator.Send(new CreateModelYearCommand(r.SubmodelId, r.Year), ct) });

    [RequirePermission(Permissions.LookupManage)]
    [HttpPut("vehicle-model-years/{id:long}")]
    public async Task<IActionResult> UpdateModelYear(long id, [FromBody] YearRequest r, CancellationToken ct)
    {
        await Mediator.Send(new UpdateModelYearCommand(id, r.Year), ct);
        return NoContent();
    }

    [RequirePermission(Permissions.LookupManage)]
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
    [RequirePermission(Permissions.QuotationRead)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetQuotationsQuery(page, pageSize, search), ct));

    [RequirePermission(Permissions.QuotationWrite)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuotationRequest r, CancellationToken ct)
    {
        var id = await Mediator.Send(new CreateQuotationCommand(r.CustomerId, r.VehicleId, r.CoverageType, r.SumInsured), ct);
        return CreatedAtAction(null, new { id }, new { id });
    }
}

// Policies moved to FastEndpoints (REPR) — see Api/Endpoints/Policies/.

// ---------- Renewals ----------
public record RenewRequest(decimal? AdjustedSumInsured);

public class RenewalsController : ApiControllerBase
{
    [RequirePermission(Permissions.PolicyRenew)]
    [HttpPost("{policyId:long}")]
    public async Task<IActionResult> Renew(long policyId, [FromBody] RenewRequest r, CancellationToken ct)
    {
        var id = await Mediator.Send(new RenewPolicyCommand(policyId, r.AdjustedSumInsured), ct);
        return Created($"/api/policies/{id}", new { id });
    }
}

// Claims moved to FastEndpoints (REPR) — see Api/Endpoints/Claims/.

// ---------- Payments ----------
public record SettlePaymentRequest(string ReferenceNo);

public class PaymentsController : ApiControllerBase
{
    [RequirePermission(Permissions.PaymentRead)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] string? direction = null,
        [FromQuery] long? policyId = null, [FromQuery] long? claimId = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetPaymentsQuery(page, pageSize, search, status, direction, policyId, claimId), ct));

    [RequirePermission(Permissions.PaymentSettle)]
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
    [RequirePermission(Permissions.DashboardRead)]
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
        => Ok(await Mediator.Send(new GetDashboardSummaryQuery(), ct));
}
