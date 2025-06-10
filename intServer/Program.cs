using Makaretu.Dns;
using intShared;
using intServer;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
var app = builder.Build();

var httpClient = new HttpClient()
{
    Timeout = TimeSpan.FromSeconds(10)
};
var info = new EquipmentInfo
{
    ID = Guid.NewGuid(),
    FriendlyName = app.Configuration.GetValue<string>(nameof(EquipmentInfo.FriendlyName)) ?? string.Empty,
    Type = app.Configuration.GetValue<string>(nameof(EquipmentInfo.Type)) ?? string.Empty,
    UnitsSupported = app.Configuration.GetValue<bool>(nameof(EquipmentInfo.UnitsSupported)),
    PacksSupported = app.Configuration.GetValue<bool>(nameof(EquipmentInfo.PacksSupported))
};
var minimalQuantity = app.Configuration.GetValue<int>("MinimalQuantity");
var discoveredServices = new ConcurrentDictionary<Guid, EquipmentWithAddress>();
var medicineInfo = Mocks.Medications();
var medicineStock = Mocks.MedicationStock(medicineInfo, info.UnitsSupported ?? false, info.PacksSupported ?? false);

Console.WriteLine($"{info.FriendlyName} with id {info.ID} starting...");

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/health", () => Results.Ok())
.WithName("Health");

app.MapGet("/info", () => Results.Json(info))
.WithName("Info");

app.MapPost("/dispense", (MedicationTransaction request) =>
{
    var unitsSuppoorted = info.UnitsSupported ?? false;
    var packsSupported = info.PacksSupported ?? false;

    if (!unitsSuppoorted && !packsSupported)
        return Results.UnprocessableEntity("Device does not support dispensing");

    Console.WriteLine($"Dispensing request recieved from {request.Sender.FriendlyName}.");

    if (request.Medications.Count == 0)
        return Results.BadRequest("No medications in dispense request");
    if (!unitsSuppoorted && request.Medications.Any(a => !a.WholePack))
        return Results.BadRequest("Dispensing individial quantity is not supported");
    if (!packsSupported && request.Medications.Any(a => a.WholePack))
        return Results.BadRequest("Dispensing packs is not supported");

    var dispensedMedication = new List<Medication>();
    
    foreach (var req in request.Medications)
    {
        var medicineDispense = medicineStock.Where(w => w.PC == req.PC);
        if (!string.IsNullOrEmpty(req.Batch))
            medicineDispense = medicineDispense.Where(w => w.Batch == req.Batch);
        if (!string.IsNullOrEmpty(req.SN))
            medicineDispense = medicineDispense.Where(w => w.SN == req.SN);

        var medicine = medicineDispense.OrderBy(o => o.Exp).FirstOrDefault();
        if (medicine is null)
            continue;

        if (medicine.UnitQuantity == req.UnitQuantity)
        {
            dispensedMedication.Add(medicine);
            medicineStock.Remove(medicine);
        }
        else
        {
            req.WholePack = false;
            dispensedMedication.Add(req);
            medicine.UnitQuantity -= req.UnitQuantity;
        }
    }

    if (dispensedMedication.Count == 0)
    {
        var response = $"No medications dispensed for {request.Sender.FriendlyName}.";
        Console.WriteLine(response);
        return Results.NotFound(response);
    }

    Console.WriteLine($"Successfully dispensed {dispensedMedication.Count} medications to {request.Sender.FriendlyName}.");

    _ = Helpers.RestockAsync(httpClient, medicineStock, discoveredServices, minimalQuantity, info);

    return Results.Json(new MedicationTransaction()
    {
        Id = request.Id,
        Sender = info,
        Medications = dispensedMedication
    });
})
.WithName("Dispense");

app.MapGet("/inventory", (string? PC) => 
{
    var stock = new List<Medication>();
    if (!string.IsNullOrEmpty(PC))
        stock = [.. medicineStock.Where(w => w.PC == PC)];
    else
        stock = medicineStock;

    if (stock.Count == 0)
        return Results.NotFound("No stock found");

    var response = new MedicationTransaction()
    {
        Id = Guid.CreateVersion7(),
        Sender = info,
        Medications = stock
    };

    return Results.Json(response);
})
.WithName("Inventory");

var service = new ServiceProfile(info.ID.ToString(), "_interopearbility._tcp", app.Configuration.GetValue<ushort>("Port"));
var sd = new ServiceDiscovery();
sd.ServiceInstanceDiscovered += async (s, e) =>
{
    await Helpers.ParseDiscoveredService(e, discoveredServices, info, httpClient);
};
if (sd.Probe(service))
{
    Console.WriteLine($"Service discovery already exists for service {service.HostName}");
}
else
{
    sd.Advertise(service);
    sd.Announce(service, 1);
}
sd.QueryServiceInstances("_interopearbility._tcp");

app.Run();