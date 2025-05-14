using intShared;
using Makaretu.Dns;

namespace intServer;

public static class Helpers
{
    public static async Task ParseDiscoveredService(ServiceInstanceDiscoveryEventArgs serviceInstance, List<EquipmentWithAddress> dicoveredServices, EquipmentInfo thisInfo, HttpClient httpClient)
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

                    var oldService = dicoveredServices.FirstOrDefault(f => f.ID == serviceInfo.ID);
                    if (oldService is not null)
                        dicoveredServices.Remove(oldService);

                    dicoveredServices.Add(serviceInfo);
                    Console.WriteLine($"Discovored service {serviceInfo.ID} on address {serviceInfo.IpAddresses[0]}:{serviceInfo.Port}.");
                }
            }
        }
    }

    public static async Task RestockAsync(HttpClient client, List<Medication> stock, List<EquipmentWithAddress> discoveredServices, int minimalQuantity, EquipmentInfo thisInfo)
    {
        if (stock.Any(a => !a.WholePack && a.UnitQuantity < minimalQuantity))
            return;
        
        var restockingServices = discoveredServices.Where(w => w.PacksSupported ?? false);
        var emptyMedicine = stock.Where(w => !w.WholePack && w.UnitQuantity < minimalQuantity);

        foreach (var medicine in emptyMedicine)
        {
            Console.WriteLine($"Restocking medicine PC: {medicine.PC} ...");
            Medication? newMedicine = null;

            foreach (var service in restockingServices)
            {
                try
                {
                    var response = await client.GetAsync($"http://{service.IpAddresses}:{service.Port}/inventory?PC={medicine.PC}");
                    var inventory = await response.Content.ReadFromJsonAsync<MedicationTransaction>();
                    if (inventory?.Medications.Any(a => a.WholePack) ?? false)
                    {
                        var request = new MedicationTransaction()
                        {
                            Id = Guid.CreateVersion7(),
                            Sender = thisInfo,
                            Medications = [inventory.Medications.Where(w => w.WholePack).OrderBy(o => o.Exp).First()],
                        };
                        var dispenseResponse = await client.PostAsJsonAsync($"http://{service.IpAddresses}:{service.Port}/dispense", request);
                        var dispenseResult = await dispenseResponse.Content.ReadFromJsonAsync<MedicationTransaction>();

                        if (dispenseResult?.Medications.Any() ?? false)
                        {
                            newMedicine = dispenseResult.Medications.First();
                            if (newMedicine is not null)
                            {
                                newMedicine.WholePack = false;
                                stock.Add(newMedicine);
                                Console.WriteLine($"Restocked {newMedicine.PC}, with quantity {newMedicine.UnitQuantity} from {service.FriendlyName}.");
                            }
                            else
                            {
                                Console.WriteLine($"Unable to restock medicine PC: {medicine.PC}.");
                            }

                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Restock error for service {service.FriendlyName} with message {ex.Message}");
                    discoveredServices.Remove(service);
                }
            }
        }
    }
}