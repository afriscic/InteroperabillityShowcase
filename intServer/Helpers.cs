using intShared;
using Makaretu.Dns;
using System.Collections.Concurrent;

namespace intServer;

public static class Helpers
{
    public static async Task ParseDiscoveredService(ServiceInstanceDiscoveryEventArgs serviceInstance, ConcurrentDictionary<Guid, EquipmentWithAddress> dicoveredServices, EquipmentInfo thisInfo, HttpClient httpClient)
    {
        if (serviceInstance.ServiceInstanceName.Labels.Any(a => a.Contains("interopearbility")) &&
        Guid.TryParse(serviceInstance.ServiceInstanceName.Labels[0], out var instanceId) &&
        instanceId != thisInfo.ID)
        {
            var newService = new EquipmentWithAddress() { ID = instanceId };

            var aRecords = serviceInstance.Message.Answers.OfType<ARecord>().Concat(serviceInstance.Message.AdditionalRecords.OfType<ARecord>());
            foreach (var aRecord in aRecords)
            {
                newService.IpAddresses.Add(aRecord.Address.ToString());
            }

            var srvRecort = serviceInstance.Message.Answers.OfType<SRVRecord>().Concat(serviceInstance.Message.AdditionalRecords.OfType<SRVRecord>()).FirstOrDefault();
            if (srvRecort is not null)
                newService.Port = srvRecort.Port;

            if (newService.IpAddresses.Count > 0 && newService.Port > 0)
            {
                EquipmentWithAddress? serviceInfo = null;
                foreach (var address in newService.IpAddresses)
                {
                    try
                    {
                        serviceInfo = await httpClient.GetFromJsonAsync<EquipmentWithAddress>($"http://{address}:{newService.Port}/info");
                    }
                    catch
                    {
                        newService.IpAddresses.Remove(address);
                    }
                }
                
                if (serviceInfo is not null)
                {
                    serviceInfo.IpAddresses = newService.IpAddresses;
                    serviceInfo.Port = newService.Port;

                    dicoveredServices.Remove(serviceInfo.ID, out _);
                    dicoveredServices.TryAdd(serviceInfo.ID, serviceInfo);
                    Console.WriteLine($"Discovored service {serviceInfo.FriendlyName} on address {serviceInfo.IpAddresses[0]}:{serviceInfo.Port}.");
                }
            }
        }
    }

    public static async Task RestockAsync(HttpClient client, List<Medication> stock, ConcurrentDictionary<Guid, EquipmentWithAddress> discoveredServices, int minimalQuantity, EquipmentInfo thisInfo)
    {
        var restockingServices = discoveredServices.Where(w => w.Value.PacksSupported ?? false).Select(s => s.Value);
        var emptyMedicine = stock.Where(w => !w.WholePack).GroupBy(g => g.PC).Where(g => g.Sum(x => x.UnitQuantity) < minimalQuantity).Select(s => s.Key);

        foreach (var medicine in emptyMedicine)
        {
            Console.WriteLine($"Restocking medicine PC: {medicine} ...");
            Medication? newMedicine = null;

            foreach (var service in restockingServices)
            {
                try
                {
                    var response = await client.GetAsync($"http://{service.IpAddresses[0]}:{service.Port}/inventory?PC={medicine}");
                    var inventory = await response.Content.ReadFromJsonAsync<MedicationTransaction>();
                    if (inventory?.Medications.Any(a => a.WholePack) ?? false)
                    {
                        var request = new MedicationTransaction()
                        {
                            Id = Guid.CreateVersion7(),
                            Sender = thisInfo,
                            Medications = [inventory.Medications.Where(w => w.WholePack).OrderBy(o => o.Exp).First()],
                        };
                        var dispenseResponse = await client.PostAsJsonAsync($"http://{service.IpAddresses[0]}:{service.Port}/dispense", request);
                        var dispenseResult = await dispenseResponse.Content.ReadFromJsonAsync<MedicationTransaction>();

                        Console.WriteLine($"Revieved dispensing conformation from {service.FriendlyName}.");

                        await Task.Delay(5000);

                        if (dispenseResult is not null && dispenseResult?.Medications.Count > 0)
                        {
                            newMedicine = dispenseResult.Medications.First();
                            if (newMedicine is not null)
                            {
                                newMedicine.WholePack = false;
                                stock.Add(newMedicine);
                                Console.WriteLine($"Restocked {newMedicine.PC} with quantity {newMedicine.UnitQuantity} from {service.FriendlyName}.");
                            }
                            else
                            {
                                Console.WriteLine($"Unable to restock medicine PC: {medicine}.");
                            }

                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (service is null)
                        return;

                    Console.WriteLine($"Restock error for service {service.FriendlyName} with message {ex.Message}");
                    discoveredServices.Remove(service.ID, out _);
                }
            }
        }
    }
}