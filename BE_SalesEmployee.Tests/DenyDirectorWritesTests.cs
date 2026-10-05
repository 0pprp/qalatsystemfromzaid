using BE_SalesEmployee.Sales.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class DenyDirectorWritesTests
{
    [Fact]
    public async Task Director_post_without_allow_is_forbidden()
    {
        var ctx = BuildContext(SalesRoles.UserTypeDirector, HttpMethods.Post, allowReadPost: false);
        var filter = new DenyDirectorWritesAttribute();
        await filter.OnActionExecutionAsync(ctx, () => Task.FromResult<ActionExecutedContext>(null!));
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task Director_evaluate_allow_read_post_passes()
    {
        var nextCalled = false;
        var ctx = BuildContext(SalesRoles.UserTypeDirector, HttpMethods.Post, allowReadPost: true);
        var filter = new DenyDirectorWritesAttribute();
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(ctx, ctx.Filters.ToList(), ctx.Controller));
        });
        Assert.True(nextCalled);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task Sales_manager_post_passes()
    {
        var nextCalled = false;
        var ctx = BuildContext(SalesRoles.UserTypeSalesManager, HttpMethods.Post, allowReadPost: false);
        var filter = new DenyDirectorWritesAttribute();
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(ctx, ctx.Filters.ToList(), ctx.Controller));
        });
        Assert.True(nextCalled);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task Director_get_passes()
    {
        var nextCalled = false;
        var ctx = BuildContext(SalesRoles.UserTypeDirector, HttpMethods.Get, allowReadPost: false);
        var filter = new DenyDirectorWritesAttribute();
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(ctx, ctx.Filters.ToList(), ctx.Controller));
        });
        Assert.True(nextCalled);
    }

    static ActionExecutingContext BuildContext(string userType, string httpMethod, bool allowReadPost)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = httpMethod;
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("UserType", userType),
        }, "test"));

        var filters = new List<IFilterMetadata>();
        if (allowReadPost)
            filters.Add(new AllowDirectorReadPostAttribute());
        filters.Add(new DenyDirectorWritesAttribute());

        var action = new ActionContext(http, new RouteData(), new ActionDescriptor
        {
            EndpointMetadata = allowReadPost
                ? new object[] { new AllowDirectorReadPostAttribute() }
                : Array.Empty<object>(),
        });

        return new ActionExecutingContext(action, filters, new Dictionary<string, object?>(), controller: new object());
    }
}
