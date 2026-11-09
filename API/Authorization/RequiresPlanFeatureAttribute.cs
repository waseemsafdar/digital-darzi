using Application.Common;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Authorization;

/// <summary>Returns 402 with a readable reason when the business's current plan leaves this feature out.</summary>
public sealed class RequiresPlanFeatureAttribute : TypeFilterAttribute
{
    public RequiresPlanFeatureAttribute(PlanFeature feature) : base(typeof(PlanFeatureFilter))
    {
        Arguments = new object[] { feature };
    }

    private sealed class PlanFeatureFilter : IAsyncActionFilter
    {
        private readonly PlanFeature _feature;
        private readonly IPlanLimitService _limits;

        public PlanFeatureFilter(PlanFeature feature, IPlanLimitService limits)
        {
            _feature = feature;
            _limits = limits;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var reason = await _limits.CheckFeatureAsync(_feature, context.HttpContext.RequestAborted);
            if (reason != null)
            {
                context.Result = new ObjectResult(ApiResponse<object>.PaymentRequired(reason)) { StatusCode = StatusCodes.Status402PaymentRequired };
                return;
            }
            await next();
        }
    }
}
