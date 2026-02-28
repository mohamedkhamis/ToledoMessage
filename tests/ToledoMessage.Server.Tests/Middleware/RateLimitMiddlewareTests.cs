using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ToledoMessage.Middleware;
using ToledoMessage.Services;

namespace ToledoMessage.Server.Tests.Middleware;

[TestClass]
public class RateLimitMiddlewareTests
{
    private static (RateLimitMiddleware middleware, RateLimitService service) CreateMiddleware(
        RequestDelegate? next = null)
    {
        var service = new RateLimitService();
        next ??= _ => Task.CompletedTask;
        return (new RateLimitMiddleware(next, service), service);
    }

    [TestMethod]
    public async Task InvokeAsync_NullPath_PassesThrough()
    {
        bool nextCalled = false;
        var (middleware, _) = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var context = new DefaultHttpContext();
        context.Request.Path = new PathString();

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_NonRateLimitedPath_PassesThrough()
    {
        bool nextCalled = false;
        var (middleware, _) = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/devices";
        context.Connection.RemoteIpAddress = IPAddress.Loopback;

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_RegisterEndpoint_LimitsAfterExceeded()
    {
        var (middleware, _) = CreateMiddleware();

        for (int i = 0; i < 5; i++)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/auth/register";
            context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
            await middleware.InvokeAsync(context);
            Assert.AreNotEqual(429, context.Response.StatusCode);
        }

        // 6th request should be rate limited
        var limitedContext = new DefaultHttpContext();
        limitedContext.Request.Path = "/api/auth/register";
        limitedContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        limitedContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(limitedContext);

        Assert.AreEqual(429, limitedContext.Response.StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_ByUserRoute_NoAuthUser_PassesThrough()
    {
        bool nextCalled = false;
        var (middleware, _) = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/messages";
        // No authenticated user

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_ByUserRoute_WithAuth_RateLimits()
    {
        var (middleware, _) = CreateMiddleware();

        for (int i = 0; i < 60; i++)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/messages";
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "user1")], "Test"));
            await middleware.InvokeAsync(context);
        }

        // 61st request should be rate limited
        var limitedContext = new DefaultHttpContext();
        limitedContext.Request.Path = "/api/messages";
        limitedContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user1")], "Test"));
        limitedContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(limitedContext);

        Assert.AreEqual(429, limitedContext.Response.StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_SearchEndpoint_LimitsAt10()
    {
        var (middleware, _) = CreateMiddleware();

        for (int i = 0; i < 10; i++)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/users/search";
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "searcher")], "Test"));
            await middleware.InvokeAsync(context);
        }

        var limitedContext = new DefaultHttpContext();
        limitedContext.Request.Path = "/api/users/search";
        limitedContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "searcher")], "Test"));
        limitedContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(limitedContext);

        Assert.AreEqual(429, limitedContext.Response.StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_DifferentIPs_IndependentLimits()
    {
        var (middleware, _) = CreateMiddleware();

        // Exhaust limit for IP 1
        for (int i = 0; i < 6; i++)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/auth/register";
            context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
            context.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(context);
        }

        // IP 2 should still be allowed
        var context2 = new DefaultHttpContext();
        context2.Request.Path = "/api/auth/register";
        context2.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.2");

        await middleware.InvokeAsync(context2);

        Assert.AreNotEqual(429, context2.Response.StatusCode);
    }
}
