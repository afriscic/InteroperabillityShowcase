namespace intShared;

public class Medication
{
    public required string PC { get; set; }
    public string? Batch { get; set; }
    public DateTime? Exp { get; set; }
    public string? SN { get; set; }
    public int UnitQuantity { get; set; }
    public bool WholePack { get; set; }
}