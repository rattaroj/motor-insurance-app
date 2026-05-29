using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MotorInsurance.Api.Authorization;

/// <summary>Policy-name prefix for dynamically-generated permission policies.</summary>
internal static class PermissionPolicy
{
    public const string Prefix = "perm:";
    public static string For(string permission) => Prefix + permission;
}

/// <summary>
/// Requires the caller to hold a specific permission claim ("perm"). Usage:
/// <c>[RequirePermission(Permissions.PolicyIssue)]</c>. Backed by the dynamic
/// <see cref="PermissionPolicyProvider"/> + <see cref="PermissionAuthorizationHandler"/>.
/// </summary>
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission) => Policy = PermissionPolicy.For(permission);
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("perm", requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Creates a policy on demand for any "perm:&lt;code&gt;" name so we don't have to register
/// one policy per permission. Falls back to the default provider for other policy names.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
        {
            var permission = policyName[PermissionPolicy.Prefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
