using System.Text;
using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using MotorInsurance.Api;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Infrastructure;
using MotorInsurance.Infrastructure.Identity;
using MotorInsurance.Infrastructure.Persistence;
using MotorInsurance.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();

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
