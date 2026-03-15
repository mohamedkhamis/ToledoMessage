using Toledo.SharedKernel.Helpers;

namespace ToledoVault.Server.Tests.Helpers;

[TestClass]
public class IdGeneratorTests
{
    [TestMethod]
    public void GetNewId_ReturnsNonZero()
    {
        var id = IdGenerator.GetNewId();
        Assert.AreNotEqual(0L, id);
    }

    [TestMethod]
    public void GetNewId_ReturnsPositive()
    {
        var id = IdGenerator.GetNewId();
        Assert.IsTrue(id > 0);
    }

    [TestMethod]
    public void GetNewId_ReturnsUniqueIds()
    {
        var ids = new HashSet<long>();
        for (int i = 0; i < 1000; i++) ids.Add(IdGenerator.GetNewId());
        Assert.AreEqual(1000, ids.Count);
    }

    [TestMethod]
    public void GetNewId_IsThreadSafe()
    {
        var ids = new System.Collections.Concurrent.ConcurrentBag<long>();
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 100; i++) ids.Add(IdGenerator.GetNewId());
        }));

        Task.WaitAll(tasks.ToArray());

        Assert.AreEqual(1000, ids.Distinct().Count());
    }
}
