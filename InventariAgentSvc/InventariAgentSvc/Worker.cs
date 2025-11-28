using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using InventariAgentSvc.Config;
using InventariAgentSvc.Services;
using System.Collections.Generic;
using System.IO;

namespace InventariAgentSvc;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly MetricsCollector _metricsCollector;
    private readonly FirebaseClient _firebaseClient;

                if (success)
                {
                    // El servicio se detendrá y reiniciará automáticamente
                    Environment.Exit(0);
                }
                else
                {
                    try
                    {
                        await _firebaseClient.SetUpdateStatusAsync(
                            _configStore.Config.DeviceId, 
                            "failed", 
                            "Error durante la actualización"
                        );
                    }
                    catch { /* Ignorar errores de notificación */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando actualizaciones en GitHub");
        }
    }


    private async Task CheckRemoteExamModeAsync()
    {
        try
        {
            var remoteExamMode = await _firebaseClient.GetExamModeAsync(_configStore.Config.DeviceId);
            
            // Si el estado remoto es diferente al local, actualizamos
            if (remoteExamMode != _configStore.Config.ExamMode)
            {
                _logger.LogInformation("Sincronizando Modo Examen desde remoto: {State}", remoteExamMode);
                _configStore.Config.ExamMode = remoteExamMode;
                await _configStore.SaveAsync();

                // Aplicar cambios
                if (remoteExamMode)
                {
                    _hostsBlocker.EnableExamMode();
                }
                else
                {
                    _hostsBlocker.DisableExamMode();
                }
            }
            // Asegurar consistencia (por si se modificó el hosts manualmente)
            else if (_configStore.Config.ExamMode)
            {
                 _hostsBlocker.EnableExamMode();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sincronizando Modo Examen");
        }
    }
}
