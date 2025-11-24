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
builder.Services.AddTransient<DeviceSetupMenu>();

// Primero verificar configuración antes de agregar el servicio
var tempProvider = builder.Services.BuildServiceProvider();
var configStore = tempProvider.GetRequiredService<ConfigStore>();

try
{
    // Verificar si el dispositivo está configurado
    if (string.IsNullOrEmpty(configStore.Config.DeviceId))
    {
        Console.WriteLine("No hay un dispositivo configurado.");
        Console.WriteLine("Iniciando menú de configuración...\n");
        
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
        
        Console.WriteLine("\nDispositivo configurado correctamente.");
        Console.WriteLine("Saliendo del configurador. El servicio se iniciará como servicio de Windows.");
        Console.WriteLine("Presiona cualquier tecla para continuar...");
        Console.ReadKey();
        return; // Salir sin iniciar el servicio, se iniciará desde el instalador
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\nError durante la configuración: {ex.Message}");
    Console.WriteLine($"\nDetalles completos:");
    Console.WriteLine(ex.ToString());
    Console.WriteLine("\nPresiona cualquier tecla para salir...");
    Console.ReadKey();
    return;
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
