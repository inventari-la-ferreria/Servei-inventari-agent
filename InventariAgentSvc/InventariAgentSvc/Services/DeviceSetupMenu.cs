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
                Console.WriteLine("Por favor, introduzca el ID del PC (ejemplo: PC-DEBUG-001)");
                Console.WriteLine("IDs disponibles:");
                Console.WriteLine();

                foreach (var device in deviceList)
                {
                    var selected = device.Id == _configStore.Config.DeviceId ? " [SELECCIONADO]" : "";
                    Console.WriteLine($"- {device.Id}{selected}");
                }

                Console.WriteLine("\n0. Salir");
                Console.WriteLine();
                Console.Write("ID del PC: ");

                var input = Console.ReadLine();
                
                if (input == "0")
                {
                    return false;
                }

                var selectedDevice = deviceList.FirstOrDefault(d => d.Id.Equals(input, StringComparison.OrdinalIgnoreCase));
                
                if (selectedDevice == null)
                {
                    Console.WriteLine("ID de PC no encontrado. Presione cualquier tecla para continuar...");
                    Console.ReadKey();
                    continue;
                }

                _configStore.Config.DeviceId = selectedDevice.Id;
                _configStore.Config.DeviceName = selectedDevice.Name;
                await _configStore.SaveAsync();

                Console.WriteLine($"\nDispositivo seleccionado: {selectedDevice.Name}");
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
            var device = new DeviceInfo
            {
                Id = doc.Id,
                Name = doc.GetValue<string>("tag") ?? doc.Id,
                Location = doc.GetValue<string>("location"),
                Status = doc.GetValue<string>("status") ?? "desconocido",
                Tag = doc.GetValue<string>("tag")
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