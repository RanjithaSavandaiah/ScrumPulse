namespace ScrumPulse.Api.Middleware;

using ScrumPulse.Application.Common.Interfaces;

/// <summary>
/// Tenant resolution middleware inspecting incoming requests for team context via:
/// 1. Header: X-Team-Id
/// 2. Query parameter: ?teamId=
/// 3. Cookie: ScrumPulse_TeamId
/// </summary>
public class TenantMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Team-Id";
    public const string QueryParamName = "teamId";
    public const string CookieName = "ScrumPulse_TeamId";

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // 1. Check HTTP Header
        if (context.Request.Headers.TryGetValue(HeaderName, out var headerVal) &&
            Guid.TryParse(headerVal.FirstOrDefault(), out var headerGuid) &&
            headerGuid != Guid.Empty)
        {
            tenantContext.CurrentTeamId = headerGuid;
        }
        // 2. Check Query String
        else if (context.Request.Query.TryGetValue(QueryParamName, out var queryVal) &&
                 Guid.TryParse(queryVal.FirstOrDefault(), out var queryGuid) &&
                 queryGuid != Guid.Empty)
        {
            tenantContext.CurrentTeamId = queryGuid;
        }
        // 3. Check Cookie
        else if (context.Request.Cookies.TryGetValue(CookieName, out var cookieVal) &&
                 Guid.TryParse(cookieVal, out var cookieGuid) &&
                 cookieGuid != Guid.Empty)
        {
            tenantContext.CurrentTeamId = cookieGuid;
        }

        // Resolve Operator / User Identity for Audit Stamping (CreatedBy / UpdatedBy)
        if (context.Request.Headers.TryGetValue("X-User-Name", out var userHeader) &&
            !string.IsNullOrWhiteSpace(userHeader.FirstOrDefault()))
        {
            tenantContext.CurrentUser = userHeader.FirstOrDefault()!.Trim();
        }
        else if (context.Request.Headers.TryGetValue("X-User-Role", out var roleHeader) &&
                 !string.IsNullOrWhiteSpace(roleHeader.FirstOrDefault()))
        {
            tenantContext.CurrentUser = roleHeader.FirstOrDefault()!.Trim();
        }
        else if (context.Request.Cookies.TryGetValue("ScrumPulse_User", out var cookieUser) &&
                 !string.IsNullOrWhiteSpace(cookieUser))
        {
            tenantContext.CurrentUser = cookieUser.Trim();
        }
        else
        {
            tenantContext.CurrentUser = "Scrum Master";
        }

        await next(context);
    }
}
