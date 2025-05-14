using System.Collections.ObjectModel;
using System.Net.Http.Json;
using intShared;
using Zeroconf;

namespace intClient.Components;

class HomePageState
{
    public ObservableCollection<EquipmentWithAddress> DiscoveredServices { get; set; } = [];
    public bool Appeared { get; set; } = true;
}

partial class HomePage : Component<HomePageState>
{
    [Inject]
    private readonly HttpClient httpClient;

    public override VisualNode Render() => 
        NavigationPage(
            ContentPage(
                CollectionView().ItemsSource(State.DiscoveredServices, RenderServer) 
            )
            .OnAppearing(() => State.Appeared = true)
            .OnDisappearing(() => State.Appeared = false)
        )
        .Title("Devices")
        .BarTextColor(Colors.SlateGray);
    
    private VisualNode RenderServer(EquipmentWithAddress server) =>
        Border(
            Grid("Auto, Auto, Auto", "*, *,",
                Label(server.FriendlyName).GridRow(0).GridColumnSpan(2).FontSize(24).FontAttributes(FontAttributes.Bold),
                Label($"Type:{server.Type}").GridRow(1).GridColumn(0),
                Label($"ID: {server.ID}").GridRow(1).GridColumn(1),
                Label($"Pack supported: {server.PacksSupported}").GridRow(2).GridColumn(0),
                Label($"Unit dose supported: {server.UnitsSupported}").GridRow(2).GridColumn(1)
            )
            .RowSpacing(5)
        )
        .Padding(16)
        .Margin(8)
        .Shadow(new MauiReactor.Shadow().Brush(Brush.DimGray).Offset(5, 5).Radius(10))
        .BackgroundColor(Colors.White)
        .OnTapped(async () => await Navigation.PushAsync<StockPage, StockPageProps>(p => p.Server = server));

#if !IOS && !MACCATALYST
    protected override async void OnMounted()
    {
        // For iOS and MacCatalyst it needs com.apple.developer.networking.multicast entitlement.
        await ZeroconfResolver.ListenForAnnouncementsAsync(async (s) => await ParseHost(s.Host), default);
        await ResolveDomainAsync();
        base.OnMounted();
    }
# else
    protected override void OnMounted()
    {
        _ = Task.Run(async () => 
        {
            while (true)
            {
                if (State.Appeared)          
                    await MainThread.InvokeOnMainThreadAsync(ResolveDomainAsync);

                await Task.Delay(TimeSpan.FromSeconds(15));
            }
        });
        base.OnMounted();
    }
#endif

    private async Task ResolveDomainAsync()
    {
        try
        {
            var hosts = await ZeroconfResolver.ResolveAsync("_interopearbility._tcp");
            if (hosts is null)
                return;

            foreach (var host in hosts)
            {
                await ParseHost(host);
            }
        }
        catch { }
    }

    private async Task ParseHost(IZeroconfHost host)
    {
        if (Guid.TryParse(host.DisplayName, out var id) && host.Services.Any(s => s.Key.Contains("interopearbility")))
        {
            var port = host.Services.First(f => f.Key.Contains(host.DisplayName)).Value.Port;
            var addresses = host.IPAddresses.ToList();

            EquipmentWithAddress? response = null;
            foreach (var address in addresses)
            {
                try
                {
                    response = await httpClient.GetFromJsonAsync<EquipmentWithAddress>($"http://{address}:{port}/info");
                }
                catch 
                {
                    addresses.Remove(address);
                }
            }
            
            if (response is null)
                return;
            
            response.IpAddresses = addresses;
            response.Port = port;

            var current = State.DiscoveredServices.FirstOrDefault(f => f.ID == response.ID);
            if (current is not null)
                State.DiscoveredServices.Remove(current);

            SetState(s => s.DiscoveredServices.Add(response));
        }
    }
}
