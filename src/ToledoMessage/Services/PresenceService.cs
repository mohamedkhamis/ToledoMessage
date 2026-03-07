using System.Collections.Concurrent;

// ReSharper disable RemoveRedundantBraces

namespace ToledoMessage.Services;

public class PresenceService
{
    private readonly ConcurrentDictionary<decimal, HashSet<string>> _userConnections = new();

    private readonly Lock _lock = new();

    public void AddConnection(decimal userId, string connectionId)
    {
        lock (_lock)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                connections.Add(connectionId);
            }
            else
            {
                _userConnections[userId] = [connectionId];
            }
        }
    }

    public bool RemoveConnection(decimal userId, string connectionId)
    {
        lock (_lock)
        {
            if (!_userConnections.TryGetValue(userId, out var connections))
                return false;

            connections.Remove(connectionId);
            // ReSharper disable once InvertIf
            if (connections.Count == 0)
            {
                _userConnections.TryRemove(userId, out _);
                return true;
            }

            return false;
        }
    }

    public bool IsOnline(decimal userId)
    {
        lock (_lock)
        {
            return _userConnections.TryGetValue(userId, out var connections) && connections.Count > 0;
        }
    }

    // ReSharper disable once UnusedMember.Global
    public IReadOnlyCollection<decimal> GetOnlineUserIds()
    {
        lock (_lock)
        {
            return _userConnections.Keys.ToList().AsReadOnly();
        }
    }
}
