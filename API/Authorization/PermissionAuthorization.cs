using System.Security.Claims;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace API.Authorization;

/// <summary>Declares the permission an endpoint needs: <c>[HasPermission(Permissions.OrdersCreate)]</c>.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string Prefix = "perm:";
    public HasPermissionAttribute(string permission) : base(Prefix + permission) { }
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;
    public string Permission { get; }
}

/// <summary>Builds a policy on demand for any "perm:x" policy name; defers everything else.</summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.Prefix, StringComparison.Ordinal))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[HasPermissionAttribute.Prefix.Length..]))
                .Build();
        }
        return await base.GetPolicyAsync(policyName);
    }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpContextAccessor _http;

    public PermissionAuthorizationHandler(IServiceScopeFactory scopes, IHttpContextAccessor http)
    {
        _scopes = scopes;
        _http = http;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;

        var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var tenantId);

        // Resolve from the request scope so the tenant-scoped DbContext is reused.
        var services = _http.HttpContext?.RequestServices;
        if (services != null)
        {
            var permissions = services.GetRequiredService<IPermissionService>();
            if (await permissions.HasPermissionAsync(tenantId, roles, requirement.Permission)) context.Succeed(requirement);
            return;
        }

        using var scope = _scopes.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPermissionService>();
        if (await svc.HasPermissionAsync(tenantId, roles, requirement.Permission)) context.Succeed(requirement);
    }
}
