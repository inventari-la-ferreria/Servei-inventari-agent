using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace InventariAgentSvc.Services;

public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public void SendNotification(string message)
    {
        try
        {
            // Usamos msg.exe para enviar un mensaje a todas las sesiones activas (*)
            // /TIME:10 hace que se cierre solo a los 10 segundos si no se atiende
            var psi = new ProcessStartInfo
            {
                FileName = "msg",
                Arguments = $"* /TIME:10 \"{message}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(psi);
            _logger.LogInformation("Notificación enviada al usuario: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando notificación (msg.exe)");
        }
    }
}
