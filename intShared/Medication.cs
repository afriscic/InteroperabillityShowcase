namespace intShared;

public class Medication
{
    public required string PC { get; set; }
    public required string Batch { get; set; }
    public required DateTime Exp { get; set; }
    public string? SN { get; set; }
    public int UnitQuantity { get; set; }
    public bool WholePack { get; set; }
}