using Google.Cloud.Firestore;
using InventariAgentSvc.Config;

namespace InventariAgentSvc.Services;

public class DeviceSetupMenu
{
    private readonly FirebaseClient _firebaseClient;
    private readonly ConfigStore _configStore;
    private readonly ILogger<DeviceSetupMenu> _logger;

    public DeviceSetupMenu(
        FirebaseClient firebaseClient,
        ConfigStore configStore,
        ILogger<DeviceSetupMenu> logger)
    {
        _firebaseClient = firebaseClient;
        _configStore = configStore;
        _logger = logger;
    }

    public async Task<bool> ShowMenuAsync()
    {
        try
        {
            var deviceList = await GetDeviceListAsync();
            if (!deviceList.Any())
            {
                _logger.LogWarning("No se encontraron dispositivos en Firestore");
                Console.WriteLine("No hay dispositivos registrados en la base de datos.");
                return false;
            }

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== Configuración de Dispositivo ===");
                if (_configStore.Config.DeviceId != null)
                {
                    Console.WriteLine($"El dispositivo actual ({_configStore.Config.DeviceId}) no existe en la base de datos.");
                    Console.WriteLine("Por favor, seleccione un dispositivo válido.\n");
                }
                Console.WriteLine("Por favor, introduzca el TAG del PC (ejemplo: PC-00001) o el ID interno.");
                Console.WriteLine("Dispositivos disponibles (TAG | ID):");
                Console.WriteLine();

                foreach (var device in deviceList)
                {
                    var selected = device.Id == _configStore.Config.DeviceId ? " [SELECCIONADO]" : "";
                    var tagLabel = string.IsNullOrEmpty(device.Tag) ? "(sin TAG)" : device.Tag;
                    Console.WriteLine($"- {tagLabel} | {device.Id}{selected}");
                }

                Console.WriteLine("\n0. Salir");
                Console.WriteLine();
                Console.Write("ID del PC: ");

                var input = Console.ReadLine();
                
                if (input == "0")
                {
                    return false;
                }

                var selectedDevice = deviceList.FirstOrDefault(d =>
                    (!string.IsNullOrEmpty(d.Tag) && d.Tag.Equals(input, StringComparison.OrdinalIgnoreCase)) ||
                    d.Id.Equals(input, StringComparison.OrdinalIgnoreCase));
                 
                if (selectedDevice == null)
                {
                    Console.WriteLine("TAG/ID de PC no encontrado. Presione cualquier tecla para continuar...");
                    Console.ReadKey();
                    continue;
                }

                _configStore.Config.DeviceId = selectedDevice.Id;
                _configStore.Config.DeviceName = selectedDevice.Name;
                await _configStore.SaveAsync();

                Console.WriteLine($"\nDispositivo seleccionado: {selectedDevice.Name} ({selectedDevice.Id})");
                Console.WriteLine("Configuración guardada. Iniciando monitoreo...");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mostrando menú de configuración");
            Console.WriteLine("Error accediendo a la base de datos.");
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    private async Task<List<DeviceInfo>> GetDeviceListAsync()
    {
        var devices = new List<DeviceInfo>();
        var snapshot = await _firebaseClient.GetDevicesAsync();

        foreach (var doc in snapshot)
        {
            // Usar ContainsField para evitar excepciones si el campo no existe
            string? tag = doc.ContainsField("tag") ? doc.GetValue<string>("tag") : null;
            string? name = doc.ContainsField("name") ? doc.GetValue<string>("name") : null;
            string? location = doc.ContainsField("location") ? doc.GetValue<string>("location") : null;
            string? status = doc.ContainsField("status") ? doc.GetValue<string>("status") : "desconocido";
            
            // Prioridad: tag > name > ID
            string displayName = tag ?? name ?? doc.Id;
            
            var device = new DeviceInfo
            {
                Id = doc.Id,
                Name = displayName,
                Location = location,
                Status = status,
                Tag = tag
            };
            devices.Add(device);
        }

        return devices.OrderBy(d => d.Name).ToList();
    }
}

public class DeviceInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Location { get; set; }
    public string Status { get; set; } = "";
    public string? Tag { get; set; }
}
