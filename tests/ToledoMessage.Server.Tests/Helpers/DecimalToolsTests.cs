using Toledo.SharedKernel.Helpers;

namespace ToledoMessage.Server.Tests.Helpers;

public class DecimalToolsTests
{
    [Fact]
    public void GetNewId_ReturnsNonZero()
    {
        var id = DecimalTools.GetNewId();
        Assert.NotEqual(0m, id);
    }

    [Fact]
    public void GetNewId_ReturnsPositive()
    {
        var id = DecimalTools.GetNewId();
        Assert.True(id > 0);
    }

    [Fact]
    public void GetNewId_ReturnsUniqueIds()
    {
        var ids = new HashSet<decimal>();
        for (int i = 0; i < 1000; i++)
        {
            ids.Add(DecimalTools.GetNewId());
        }
        Assert.Equal(1000, ids.Count);
    }

    [Fact]
    public void GetNewId_IsThreadSafe()
    {
        var ids = new System.Collections.Concurrent.ConcurrentBag<decimal>();
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                ids.Add(DecimalTools.GetNewId());
            }
        }));

        Task.WaitAll(tasks.ToArray());

        Assert.Equal(1000, ids.Distinct().Count());
    }
}
