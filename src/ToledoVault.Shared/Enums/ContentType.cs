using System.Text.Json.Serialization;

namespace ToledoVault.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContentType
{
    Text = 0,
    Image = 1,
    Audio = 2,
    Video = 3,
    File = 4
}
