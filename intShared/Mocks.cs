using RandomString4Net;

namespace intShared;

public static class Mocks
{
    public static List<MedicationInfo> Medications()
    {
        return
        [
            new()
            {
                PC = "03856016800366",
                Name = "Amlodipin PharmaS tbl. 30x5mg",
                INN = "amlodipine",
                PackSize = 30,
                Strength = 5
            },
            new()
            {
                PC = "03856016800373",
                Name = "Amlodipin PharmaS tbl. 60x5mg",
                INN = "amlodipine",
                PackSize = 60,
                Strength = 5
            },
            new()
            {
                PC = "03850114205535",
                Name = "Sumamed amp. 5x500mg",
                INN = "azythromicine",
                PackSize = 5,
                Strength = 500
            },
        ];
    }

    public static List<Medication> MedicationStock(List<MedicationInfo> medicationInfos, bool generateUnit = true, bool generatePack = false)
    {
        List<Medication> medicationStock = [];
        var random = new Random();

        foreach (var medicationInfo in medicationInfos)
        {
            if (generateUnit)
            {
                for (int i = 0; i < random.Next(1, 3); i++)
                {
                    var year = 2026;
                    var month = random.Next(1, 12);
                    var day = DateTime.DaysInMonth(year, month);

                    medicationStock.Add(new Medication
                    {
                        PC = medicationInfo.PC,
                        Batch = RandomString.GetString(Types.NUMBERS, 8),
                        SN = RandomString.GetString(Types.ALPHANUMERIC_UPPERCASE, 15),
                        Exp = new DateTime(year, month, day),
                        UnitQuantity = random.Next(1, medicationInfo.PackSize),
                        WholePack = false
                    });
                }
            }
            if (generatePack)
            {
                for (int i = 0; i < random.Next(1, 5); i++)
                {
                    var year = 2026;
                    var month = random.Next(1, 12);
                    var day = DateTime.DaysInMonth(year, month);

                    medicationStock.Add(new Medication
                    {
                        PC = medicationInfo.PC,
                        Batch = RandomString.GetString(Types.NUMBERS, 8),
                        SN = RandomString.GetString(Types.ALPHANUMERIC_UPPERCASE, 15),
                        Exp = new DateTime(year, month, day),
                        UnitQuantity = medicationInfo.PackSize,
                        WholePack = true
                    });
                }
            }
        }

        return medicationStock;
    }
}