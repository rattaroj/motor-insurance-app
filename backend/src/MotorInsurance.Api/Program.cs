using System.Text;
using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using MotorInsurance.Api;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Services;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Infrastructure;
using MotorInsurance.Infrastructure.Identity;
using MotorInsurance.Infrastructure.Persistence;
using MotorInsurance.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF: Community license + embedded Thai font, for policy/receipt PDF generation.
MotorInsurance.Api.Documents.PdfSetup.Configure();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();

// Background policy-lifecycle maintenance (auto-expire + auto-remind). Tunable via "PolicyLifecycle".
builder.Services.Configure<PolicyLifecycleOptions>(builder.Configuration.GetSection("PolicyLifecycle"));
builder.Services.AddHostedService<PolicyLifecycleWorker>();

// Local file storage for uploaded driver ID-card images (served via UseStaticFiles below).
var webRoot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRoot);
builder.Services.AddScoped<IFileStorage>(_ => new LocalFileStorage(webRoot));

// FastEndpoints (REPR) hosts every endpoint; NSwag generates the OpenAPI doc + UI.
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(o =>
{
    o.EnableJWTBearerAuth = true;
    o.DocumentSettings = s =>
    {
        s.Title = "Motor Insurance API";
        s.Version = "v1";
    };
});

// ----- Authentication (JWT bearer) -----
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep raw claim names ("sub", "perm", ...)
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

// ----- Authorization (permission-based + require auth by default) -----
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

const string CorsPolicy = "frontend";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins("http://localhost:3000")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // needed for the httpOnly refresh-token cookie

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Serve uploaded files (driver ID-card images). Explicit provider so it works even when
// wwwroot did not exist at host-build time. Runs before auth: images are reachable by URL.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRoot),
});

// Serve the OpenAPI doc + UI before auth so the global "require authenticated" fallback
// policy doesn't gate /swagger (dev only).
if (app.Environment.IsDevelopment())
    app.UseSwaggerGen(); // NSwag: /swagger/v1/swagger.json + UI at /swagger

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api";                 // endpoints declare "policies/..." -> /api/policies/...
    c.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
    // Map FluentValidation failures onto the uniform ApiResponse error envelope.
    c.Errors.ResponseBuilder = (failures, ctx, _) =>
    {
        var errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());
        return ApiResponse.Fail("Validation failed", errors, ctx.TraceIdentifier);
    };
});

// Seed demo users (idempotent). Tolerates a not-yet-migrated DB so the app still starts.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await AuthDataSeeder.SeedAsync(db, hasher);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Auth seeding skipped (database may not be migrated yet).");
    }
}

app.Run();

// Exposed so the integration test project can boot the API in-process via WebApplicationFactory.
public partial class Program { }
