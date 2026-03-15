namespace ToledoVault.Models;

public class LocalizationOverride
{
    public long Id { get; set; }
    public string ResourceKey { get; set; } = null!;
    public string LanguageCode { get; set; } = null!;
    public string Value { get; set; } = null!;
    public bool IsNewKey { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
}
