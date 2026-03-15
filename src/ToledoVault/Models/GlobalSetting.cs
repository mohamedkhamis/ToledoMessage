namespace ToledoVault.Models;

public class GlobalSetting
{
    public long Id { get; set; }
    public string Key { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Description { get; set; }
    public string Category { get; set; } = null!;
    public string ValueType { get; set; } = null!;
    public string CurrentValue { get; set; } = null!;
    public string DefaultValue { get; set; } = null!;
    public string? ValidationRules { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
}
