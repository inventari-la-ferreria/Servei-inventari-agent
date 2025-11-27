using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using InventariAgentSvc;
using InventariAgentSvc.Config;
using InventariAgentSvc.Services;
using InventariAgentSvc.Models;
using System;
using System.Linq;

var builder = Host.CreateApplicationBuilder(args);

// Configurar logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
});

// Registrar servicios
builder.Services.AddSingleton<ConfigStore>();
builder.Services.AddSingleton<MetricsCollector>();
builder.Services.AddSingleton<FirebaseClient>();
builder.Services.AddSingleton<AppBlocker>();
builder.Services.AddSingleton<RemoteUpdateService>();
builder.Services.AddSingleton<GitHubReleaseChecker>();
builder.Services.AddSingleton<IncidentMailSender>();
builder.Services.AddTransient<DeviceSetupMenu>();

// Detectar si se está ejecutando como servicio de Windows o en modo consola
var isService = !Environment.UserInteractive;

// Si se ejecuta en modo consola (no como servicio), permitir configuración
if (!isService)
{
    var tempProvider = builder.Services.BuildServiceProvider();
    var configStore = tempProvider.GetRequiredService<ConfigStore>();

    try
    {
        // Si no hay DeviceId o se pasa argumento --configure, mostrar menú
        var showMenu = string.IsNullOrEmpty(configStore.Config.DeviceId) || 
                       args.Contains("--configure") || 
                       args.Contains("-c");

        if (showMenu)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  CONFIGURACIÓN DEL DISPOSITIVO");
            Console.WriteLine("========================================\n");
            
            if (!string.IsNullOrEmpty(configStore.Config.DeviceId))
            {
                Console.WriteLine($"Dispositivo actual: {configStore.Config.DeviceId}");
                Console.WriteLine("Puedes cambiar la configuración a continuación.\n");
            }
            else
            {
                Console.WriteLine("No hay un dispositivo configurado.");
                Console.WriteLine("Iniciando menú de configuración...\n");
            }
            
            var firebaseClient = tempProvider.GetRequiredService<FirebaseClient>();
            var menu = tempProvider.GetRequiredService<DeviceSetupMenu>();
            var deviceSelected = await menu.ShowMenuAsync();

            if (!deviceSelected)
            {
                Console.WriteLine("\nNo se seleccionó ningún dispositivo. El servicio no se iniciará.");
                Console.WriteLine("Presiona cualquier tecla para salir...");
                Console.ReadKey();
                return;
            }
            
            Console.WriteLine("\n✓ Dispositivo configurado correctamente.");
            Console.WriteLine("El servicio puede iniciarse ahora como servicio de Windows.");
            Console.WriteLine("\nPresiona cualquier tecla para salir...");
            Console.ReadKey();
            return; // Salir sin iniciar el servicio, se iniciará como servicio de Windows
        }
        else
        {
            // Ya está configurado y no se pidió reconfigurar
            Console.WriteLine($"Dispositivo configurado: {configStore.Config.DeviceId}");
            Console.WriteLine("Iniciando en modo consola para testing...");
            Console.WriteLine("(Usa --configure para cambiar el dispositivo)\n");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("\n========================================");
        Console.WriteLine("  ERROR EN LA CONFIGURACIÓN");
        Console.WriteLine("========================================");
        Console.WriteLine($"\nError: {ex.Message}");
        Console.WriteLine($"\nDetalles completos:");
        Console.WriteLine(ex.ToString());
        Console.WriteLine("\nPosibles causas:");
        Console.WriteLine("- Credenciales de Firebase no encontradas o inválidas");
        Console.WriteLine("- Sin conexión a Internet");
        Console.WriteLine("- Firestore no accesible");
        Console.WriteLine("\nPresiona cualquier tecla para salir...");
        Console.ReadKey();
        return;
    }
}

// Ahora sí, agregar el servicio Windows y ejecutar
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "InventariAgent";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
Console.WriteLine("Iniciando servicio de monitoreo...");
host.Run();
