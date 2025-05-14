namespace intShared;

public class MedicationTransaction
{
    public required Guid Id { get; set; }
    public required EquipmentInfo Sender { get; set; }
    public required List<Medication> Medications { get; set; }
}