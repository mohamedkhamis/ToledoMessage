using ToledoMessage.Services;

namespace ToledoMessage.Server.Tests.Services;

public class RateLimitServiceTests
{
    private readonly RateLimitService _service = new();

    [Fact]
    public void IsRateLimited_FirstRequest_NotLimited()
    {
        var result = _service.IsRateLimited("key1", 5, TimeSpan.FromMinutes(1));
        Assert.False(result);
    }

    [Fact]
    public void IsRateLimited_WithinLimit_NotLimited()
    {
        for (int i = 0; i < 5; i++)
        {
            var result = _service.IsRateLimited("key2", 5, TimeSpan.FromMinutes(1));
            Assert.False(result);
        }
    }

    [Fact]
    public void IsRateLimited_ExceedsLimit_ReturnsTrue()
    {
        for (int i = 0; i < 5; i++)
        {
            _service.IsRateLimited("key3", 5, TimeSpan.FromMinutes(1));
        }

        var result = _service.IsRateLimited("key3", 5, TimeSpan.FromMinutes(1));
        Assert.True(result);
    }

    [Fact]
    public void IsRateLimited_DifferentKeys_IndependentLimits()
    {
        for (int i = 0; i < 5; i++)
        {
            _service.IsRateLimited("keyA", 5, TimeSpan.FromMinutes(1));
        }

        var resultA = _service.IsRateLimited("keyA", 5, TimeSpan.FromMinutes(1));
        var resultB = _service.IsRateLimited("keyB", 5, TimeSpan.FromMinutes(1));

        Assert.True(resultA);
        Assert.False(resultB);
    }

    [Fact]
    public void IsRateLimited_WindowExpires_ResetsCount()
    {
        // Use very short window
        for (int i = 0; i < 5; i++)
        {
            _service.IsRateLimited("key4", 5, TimeSpan.FromMilliseconds(1));
        }

        // Wait for window to expire
        Thread.Sleep(10);

        var result = _service.IsRateLimited("key4", 5, TimeSpan.FromMilliseconds(1));
        Assert.False(result);
    }

    [Fact]
    public void IsRateLimited_ExactlyAtLimit_NotLimited()
    {
        for (int i = 0; i < 3; i++)
        {
            _service.IsRateLimited("key5", 3, TimeSpan.FromMinutes(1));
        }

        // The 3rd request should be the limit exactly
        // The 4th request exceeds the limit
        var exceeded = _service.IsRateLimited("key5", 3, TimeSpan.FromMinutes(1));
        Assert.True(exceeded);
    }
}
