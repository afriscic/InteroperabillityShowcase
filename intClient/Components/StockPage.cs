using System.Net.Http.Json;
using System.Text;
using intShared;

namespace intClient.Components;

class StockPageState
{
    public List<Medication> MedicationsStock { get; set; } = [];
    public List<MedicationInfo> MedicationInfos = Mocks.Medications();
    public List<MedicationView> MedicationViews = [];
}
class StockPageProps
{
    public EquipmentWithAddress? Server { get; set; }
}

partial class StockPage : Component<StockPageState, StockPageProps>
{
    [Inject]
    private readonly HttpClient httpClient;
    [Inject]
    private readonly EquipmentInfo info;

    public override VisualNode Render() => 
        ContentPage(
            ToolbarItem("Order").OnClicked(OrderMedicationsAsync),
            CollectionView().ItemsSource(State.MedicationViews, RenderStock) 
        )
        .Title("Stock");

    private VisualNode RenderStock(MedicationView medicaion) =>
        Border(
            Grid("Auto, Auto, Auto", "2*, 2*, *, *",
                Label(medicaion.Medication.Name).GridRow(0).GridColumnSpan(2).FontSize(24).FontAttributes(FontAttributes.Bold),
                Label(medicaion.Medication.INN).GridRow(1).GridColumn(0),
                Label(medicaion.Medication.PC).GridRow(2).GridColumn(0),
                Label($"Total quantity: {medicaion.TotalQuantity}").GridRow(1).GridColumn(1),
                Label($"Whole pack: {(medicaion.WholePack ? "Yes" : "No")}").GridRow(2).GridColumn(1),


                Label("Order units:").GridRow(0).GridColumn(2).IsVisible(Props?.Server?.UnitsSupported ?? false),
                Label("Order packs:").GridRow(0).GridColumn(2).IsVisible(Props?.Server?.PacksSupported ?? false),

                Entry().Text(medicaion.OrderQuantity.ToString()).IsReadOnly(true)
                .GridRowSpan(2).GridRow(1).GridColumn(2).VCenter().Margin(5),
                Stepper().Value(medicaion.OrderQuantity).Maximum(medicaion.TotalQuantity).Increment(medicaion.WholePack ? medicaion.Medication.PackSize : 1)
                .GridRowSpan(2).GridRow(1).GridColumn(3).IsEnabled((Props?.Server?.PacksSupported ?? false) || (Props?.Server?.UnitsSupported ?? false)).VCenter().Margin(5)
                .OnValueChanged(v => SetState(s => medicaion.OrderQuantity = (int)v.NewValue))
            )
            .RowSpacing(5)
        )
        .Padding(16)
        .Margin(8)
        .Shadow(new MauiReactor.Shadow().Brush(Brush.DimGray).Offset(5, 5).Radius(10))
        .BackgroundColor(Colors.White);

    protected override async void OnMounted()
    {
        await GetInventoryAsync();
        base.OnMounted();
    }

    private async Task GetInventoryAsync()
    {
        try
        {
            if (Props.Server is null)
                return;
            var response = await httpClient.GetFromJsonAsync<MedicationTransaction>($"http://{Props.Server.IpAddresses[0]}:{Props.Server.Port}/inventory");
            if (response is null)
                return;

            State.MedicationsStock = response.Medications;

            var views = new List<MedicationView>();
            foreach (var PC in State.MedicationsStock.Select(s => s.PC).Distinct())
            {
                var view = new MedicationView()
                {
                    Medication = State.MedicationInfos.First(f => f.PC == PC),
                    TotalQuantity = State.MedicationsStock.Where(w => w.PC == PC).Sum(s => s.UnitQuantity),
                    // This doesn't support mixed packs and unitdoses but could be easly added later
                    WholePack = State.MedicationsStock.First(w => w.PC == PC).WholePack
                };
                views.Add(view);
            }

            SetState(s => s.MedicationViews = views);
        }
        catch (Exception)
        { 
            
        }
    }

    private async Task OrderMedicationsAsync()
    {
        try
        {
            if (Props.Server is null)
                return;

            var orderList = new List<Medication>();
            var orders = State.MedicationViews.Where(w => w.OrderQuantity > 0);
            foreach (var order in orders)
            {
                var stock = State.MedicationsStock
                            .Where(w => w.PC == order.Medication.PC && w.WholePack == order.WholePack)
                            .OrderBy(o => o.Exp);

                if (order.WholePack)
                {
                    orderList.AddRange(stock.Take(order.OrderQuantity / order.Medication.PackSize));
                }
                else
                {
                    var end = false;
                    do
                    {
                        var first = stock.First();
                        if (first.UnitQuantity < order.OrderQuantity)
                        {
                            orderList.Add(first);
                            State.MedicationsStock.Remove(first);
                            order.OrderQuantity -= first.UnitQuantity;
                        }
                        else
                        {
                            first.UnitQuantity = order.OrderQuantity;
                            orderList.Add(first);
                            end = true;
                        }
                    } while (!end);
                }
            }

            var transaction = new MedicationTransaction
            {
                Id = Guid.CreateVersion7(),
                Sender = info,
                Medications = orderList
            };

            var response = await httpClient.PostAsJsonAsync($"http://{Props.Server.IpAddresses[0]}:{Props.Server.Port}/dispense", transaction);
            var responseTransaction = await response.Content.ReadFromJsonAsync<MedicationTransaction>();

            if (responseTransaction is null)
                return;

            var sb = new StringBuilder();
            foreach (var item in responseTransaction.Medications)
            {
                sb.AppendLine($"PC: {item.PC}, Batch: {item.Batch}, SN: {item.SN}, Quantity: {item.UnitQuantity}");
            }
            
            if (ContainerPage is not null)
                await ContainerPage.DisplayAlert($"Dispensed from: {responseTransaction.Sender.FriendlyName}", sb.ToString(), "OK");
            
        }
        catch {}
        finally
        {
            await GetInventoryAsync();
        }
    }
}

public class MedicationView()
{
    public required MedicationInfo Medication { get; set; }
    public int TotalQuantity { get; set; }
    public bool WholePack { get; set; }
    public int OrderQuantity { get; set; }
}