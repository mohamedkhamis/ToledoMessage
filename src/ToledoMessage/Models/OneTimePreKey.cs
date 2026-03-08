namespace ToledoMessage.Models;

public class OneTimePreKey
{
    // ReSharper disable  NullableWarningSuppressionIsUsed

    public long Id { get; set; }
    public long DeviceId { get; set; }
    public int KeyId { get; set; }
    public byte[] PublicKey { get; set; } = [];
    public bool IsUsed { get; set; }

    public Device Device { get; set; } = null!;
}
