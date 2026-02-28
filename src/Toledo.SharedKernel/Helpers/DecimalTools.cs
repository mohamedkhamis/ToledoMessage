namespace Toledo.SharedKernel.Helpers;

/// <summary>
/// Provides utility methods for generating unique decimal IDs.
/// Thread-safe implementation using lock for concurrent access.
/// </summary>
public static class DecimalTools
{
    private static decimal _lastId;
    private static readonly Lock Lock = new();

    /// <summary>
    /// Generates a new unique decimal ID based on current timestamp and GUID.
    /// </summary>
    /// <returns>A unique decimal identifier</returns>
    // ReSharper disable  RemoveRedundantBraces
    public static decimal GetNewId()
    {
        lock (Lock)
        {
            while (true)
            {
                var str = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 8).ToString();
                var ticks = DateTime.Now.Ticks;
                var length = 8;
                if (str.Length < 8)
                {
                    length = str.Length;
                }

                var num = decimal.Parse($"{ticks}.{str[..length]}");
                if (num == _lastId)
                {
                    continue;
                }

                _lastId = num;
                return num;
            }
        }
    }
}
