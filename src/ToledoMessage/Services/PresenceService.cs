using System.Collections.Concurrent;

namespace ToledoMessage.Services;

public class PresenceService
{
    private readonly ConcurrentDictionary<decimal, HashSet<string>> _userConnections = new();

    public void AddConnection(decimal userId, string connectionId)
    {
        _userConnections.AddOrUpdate(
            userId,
            _ => [connectionId],
            (_, connections) =>
            {
                lock (connections)
                {
                    connections.Add(connectionId);
                }

                return connections;
            });
    }

    public bool RemoveConnection(decimal userId, string connectionId)
    {
        if (!_userConnections.TryGetValue(userId, out var connections))
            return false;

        bool isNowOffline;
        lock (connections)
        {
            connections.Remove(connectionId);
            isNowOffline = connections.Count == 0;
        }

        if (isNowOffline)
            _userConnections.TryRemove(userId, out _);

        return isNowOffline;
    }

    public bool IsOnline(decimal userId)
    {
        return _userConnections.TryGetValue(userId, out var connections) && connections.Count > 0;
    }

    // ReSharper disable once UnusedMember.Global
    public IReadOnlyCollection<decimal> GetOnlineUserIds()
    {
        return _userConnections.Keys.ToList().AsReadOnly();
    }
}
