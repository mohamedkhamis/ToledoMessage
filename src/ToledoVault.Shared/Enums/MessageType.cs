using System.Text.Json.Serialization;

namespace ToledoVault.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageType
{
    PreKeyMessage = 0,
    NormalMessage = 1
}
