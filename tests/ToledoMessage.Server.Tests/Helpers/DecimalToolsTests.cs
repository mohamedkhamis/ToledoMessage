using Toledo.SharedKernel.Helpers;

namespace ToledoMessage.Server.Tests.Helpers;

[TestClass]
public class DecimalToolsTests
{
    [TestMethod]
    public void GetNewId_ReturnsNonZero()
    {
        var id = DecimalTools.GetNewId();
        Assert.AreNotEqual(0m, id);
    }

    [TestMethod]
    public void GetNewId_ReturnsPositive()
    {
        var id = DecimalTools.GetNewId();
        Assert.IsTrue(id > 0);
    }

    [TestMethod]
    public void GetNewId_ReturnsUniqueIds()
    {
        var ids = new HashSet<decimal>();
        for (int i = 0; i < 1000; i++) ids.Add(DecimalTools.GetNewId());
        Assert.AreEqual(1000, ids.Count);
    }

    [TestMethod]
    public void GetNewId_IsThreadSafe()
    {
        var ids = new System.Collections.Concurrent.ConcurrentBag<decimal>();
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 100; i++) ids.Add(DecimalTools.GetNewId());
        }));

        Task.WaitAll(tasks.ToArray());

        Assert.AreEqual(1000, ids.Distinct().Count());
    }
}
