namespace intShared;

public class EquipmentWithAddress : EquipmentInfo
{
    public List<string> IpAddresses { get; set; } = [];
    public int Port { get; set; }
}
