using System.Net.Http.Json;
using intShared;

namespace intClient.Components;

class AllStockPageState
{
    public List<MedicationTransaction> Transactions { get; set; } = [];
    public List<MedicationTotalStock> MedicationTotalStocks { get; set; } = [];
    public List<MedicationInfo> MedicationInfos = Mocks.Medications();
}

class AllStockPageProps
{
    public List<EquipmentWithAddress> Servers { get; set; } = [];
}

partial class AllStockPage : Component<AllStockPageState, AllStockPageProps>
{
    [Inject]
    private readonly HttpClient httpClient;

    public override VisualNode Render() =>
        ContentPage(
            CollectionView().ItemsSource(State.MedicationTotalStocks, RenderMedication)
        )
        .Title("Complete stock");

    private VisualNode RenderMedication(MedicationTotalStock medicaionStock) =>
        Border(
            Grid("Auto, Auto, Auto, Auto", "*, *",
                Label(medicaionStock.Medication.Name).GridRow(0).GridColumnSpan(2).FontSize(24).FontAttributes(FontAttributes.Bold),
                Label(medicaionStock.Medication.INN).GridRow(1).GridColumn(0),
                Label(medicaionStock.Medication.PC).GridRow(2).GridColumn(0),
                Label($"Total quantity: {medicaionStock.TotalQuantity}")
                    .GridRow(1).GridRowSpan(2).GridColumn(1)
                    .FontSize(18).FontAttributes(FontAttributes.Bold),
                CollectionView().GridRow(2).GridColumnSpan(2)
                    .ItemsSource(medicaionStock.EquipmentStocks, RenderEquipment)
            )
            .RowSpacing(5)
        )
        .Padding(16)
        .Margin(8)
        .Shadow(new MauiReactor.Shadow().Brush(Brush.DimGray).Offset(5, 5).Radius(10))
        .BackgroundColor(Colors.White);

    private VisualNode RenderEquipment((EquipmentInfo info, int stock) equipmentStock) =>
        Grid("Auto", "*, *",
            Label(equipmentStock.info.FriendlyName).GridRow(0).GridColumn(0),
            Label($"Stock: {equipmentStock.stock}").GridRow(0).GridColumn(1)
        ).Margin(8).Padding(8);

    protected override async void OnMounted()
    {
        foreach (var server in Props.Servers)
        {
            try
            {
                var response = await httpClient.GetFromJsonAsync<MedicationTransaction>($"http://{server.IpAddresses[0]}:{server.Port}/inventory");
                if (response is null)
                    continue;

                State.Transactions.Add(response);
            }
            catch (Exception)
            {

            }
        }

        var distinctPCs = State.Transactions.SelectMany(sm => sm.Medications).Select(s => s.PC).Distinct();

        foreach (var distinctPC in distinctPCs)
        {
            var medInfo = State.MedicationInfos.First(f => f.PC == distinctPC);
            var equipmentGroup = State.Transactions
            .GroupBy(g => g.Sender)
            .Select(g => (
                inf: g.Key,
                stock: g.SelectMany(sm => sm.Medications).Where(w => w.PC == distinctPC).Sum(s => s.UnitQuantity)
            ))
            .ToList();

            State.MedicationTotalStocks.Add(new MedicationTotalStock()
            {
                Medication = medInfo,
                EquipmentStocks = equipmentGroup
            });
        }

        base.OnMounted();
    }
}

class MedicationTotalStock
{
    public required MedicationInfo Medication { get; set; }
    public List<(EquipmentInfo info, int stock)> EquipmentStocks { get; set; } = [];
    public int TotalQuantity { get => EquipmentStocks.Sum(s => s.stock); }
}