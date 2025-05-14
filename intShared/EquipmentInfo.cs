namespace intShared;

public class EquipmentInfo
{
    public Guid ID { get; set; }
    public string? FriendlyName { get; set; }
    public string? Type { get; set; }
    public bool? UnitsSupported { get; set; }
    public bool? PacksSupported { get; set; }
}