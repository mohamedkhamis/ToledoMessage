using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ToledoVault.Middleware;
using ToledoVault.Services;

namespace ToledoVault.Server.Tests.Middleware;

[TestClass]
public class RateLimitMiddlewareTests
{
    // ReSharper disable once UnusedTupleComponentInReturnValue
    private static (RateLimitMiddleware middleware, RateLimitService service) CreateMiddleware(
        RequestDelegate? next = null)
    {
        var service = new RateLimitService();
        next ??= static _ => Task.CompletedTask;
        return (new RateLimitMiddleware(next, service), service);
    }

    [TestMethod]
    public async Task InvokeAsync_NullPath_PassesThrough()
    {
        var nextCalled = false;
        var (middleware, _) = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = new PathString()
            }
        };

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_NonRateLimitedPath_PassesThrough()
    {
        var nextCalled = false;
        var (middleware, _) = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/api/devices"
            },
            Connection =
            {
                RemoteIpAddress = IPAddress.Loopback
            }
        };

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_RegisterEndpoint_LimitsAfterExceeded()
    {
        var (middleware, _) = CreateMiddleware();

        for (var i = 0; i < 5; i++)
        {
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/api/auth/register"
                },
                Connection =
                {
                    RemoteIpAddress = IPAddress.Parse("10.0.0.1")
                }
            };
            await middleware.InvokeAsync(context);
            Assert.AreNotEqual(429, context.Response.StatusCode);
        }

        // 6th request should be rate limited
        var limitedContext = new DefaultHttpContext
        {
            Request =
            {
                Path = "/api/auth/register"
            },
            Connection =
            {
                RemoteIpAddress = IPAddress.Parse("10.0.0.1")
            },
            Response =
            {
                Body = new MemoryStream()
            }
        };

        await middleware.InvokeAsync(limitedContext);

        Assert.AreEqual(429, limitedContext.Response.StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_ByUserRoute_NoAuthUser_PassesThrough()
    {
        var nextCalled = false;
        var (middleware, _) = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/api/messages"
            }
        };
        // No authenticated user

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_ByUserRoute_WithAuth_RateLimits()
    {
        var (middleware, _) = CreateMiddleware();

        for (var i = 0; i < 60; i++)
        {
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/api/messages"
                },
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "user1")], "Test"))
            };
            await middleware.InvokeAsync(context);
        }

        // 61st request should be rate limited
        var limitedContext = new DefaultHttpContext
        {
            Request =
            {
                Path = "/api/messages"
            },
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "user1")], "Test")),
            Response =
            {
                Body = new MemoryStream()
            }
        };

        await middleware.InvokeAsync(limitedContext);

        Assert.AreEqual(429, limitedContext.Response.StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_SearchEndpoint_LimitsAt10()
    {
        var (middleware, _) = CreateMiddleware();

        for (var i = 0; i < 10; i++)
        {
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/api/users/search"
                },
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "searcher")], "Test"))
            };
            await middleware.InvokeAsync(context);
        }

        var limitedContext = new DefaultHttpContext
        {
            Request =
            {
                Path = "/api/users/search"
            },
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "searcher")], "Test")),
            Response =
            {
                Body = new MemoryStream()
            }
        };

        await middleware.InvokeAsync(limitedContext);

        Assert.AreEqual(429, limitedContext.Response.StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_DifferentIPs_IndependentLimits()
    {
        var (middleware, _) = CreateMiddleware();

        // Exhaust limit for IP 1
        for (var i = 0; i < 6; i++)
        {
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/api/auth/register"
                },
                Connection =
                {
                    RemoteIpAddress = IPAddress.Parse("10.0.0.1")
                },
                Response =
                {
                    Body = new MemoryStream()
                }
            };
            await middleware.InvokeAsync(context);
        }

        // IP 2 should still be allowed
        var context2 = new DefaultHttpContext
        {
            Request =
            {
                Path = "/api/auth/register"
            },
            Connection =
            {
                RemoteIpAddress = IPAddress.Parse("10.0.0.2")
            }
        };

        await middleware.InvokeAsync(context2);

        Assert.AreNotEqual(429, context2.Response.StatusCode);
    }
}
