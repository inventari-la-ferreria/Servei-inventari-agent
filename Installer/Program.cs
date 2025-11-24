using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace InventariInstaller
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var tempRoot = Path.Combine(Path.GetTempPath(), "InventariAgentInstaller", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);

                // Extraer payload.zip embebido
                var payloadZipPath = Path.Combine(tempRoot, "payload.zip");
                using (var payloadStream = typeof(Program).Assembly.GetManifestResourceStream("InventariInstaller.payload.zip"))
                {
                    if (payloadStream == null)
                    {
                        Console.Error.WriteLine("No se encontró el recurso payload.zip en el ejecutable.");
                        return 2;
                    }
                    using var fs = File.Create(payloadZipPath);
                    payloadStream.CopyTo(fs);
                }

                // Descomprimir
                var extractDir = Path.Combine(tempRoot, "pkg");
                ZipFile.ExtractToDirectory(payloadZipPath, extractDir);

                // Verificar si las credenciales están en el payload (embebidas durante build)
                var payloadCreds = Path.Combine(extractDir, "firebase-credentials.json");
                if (File.Exists(payloadCreds))
                {
                    Console.WriteLine("Credenciales de Firebase detectadas en el instalador.");
                }
                else
                {
                    // Si no están embebidas, buscar junto al instalador
                    var localCreds = Path.Combine(exeDir, "firebase-credentials.json");
                    if (File.Exists(localCreds))
                    {
                        try 
                        { 
                            File.Copy(localCreds, payloadCreds, overwrite: true);
                            Console.WriteLine("Credenciales copiadas desde la carpeta del instalador.");
                        } 
                        catch { /* ignore */ }
                    }
                    else
                    {
                        // Preguntar por ruta manualmente
                        Console.WriteLine("No se encontró 'firebase-credentials.json' junto al instalador.");
                        Console.Write("Si tienes el archivo, indica la ruta completa o presiona Enter para omitir: ");
                        var inputPath = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(inputPath) && File.Exists(inputPath))
                        {
                            try
                            {
                                File.Copy(inputPath, payloadCreds, overwrite: true);
                                Console.WriteLine("Credenciales copiadas.");
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("No se pudieron copiar las credenciales. Continuando sin ellas.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Continuando sin credenciales. El servicio requerirá el archivo en C:\\ProgramData\\InventariAgent\\firebase-credentials.json.");
                        }
                    }
                }

                // Ejecutar install.ps1 con elevación
                var installPs1 = Path.Combine(extractDir, "install.ps1");
                if (!File.Exists(installPs1))
                {
                    Console.Error.WriteLine("No se encontró install.ps1 dentro del paquete.");
                    return 3;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{installPs1}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = extractDir
                };

                var proc = Process.Start(psi);
                proc?.WaitForExit();
                var installCode = proc?.ExitCode ?? 0;
                
                if (installCode != 0)
                {
                    Console.Error.WriteLine("Error durante la instalación del servicio.");
                    return installCode;
                }

                // Ejecutar configuración de dispositivo
                Console.WriteLine("\n=== Configuración del Dispositivo ===");
                var configurePs1 = Path.Combine(extractDir, "configure-device.ps1");
                if (File.Exists(configurePs1))
                {
                    var configPsi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{configurePs1}\" -InstallPath 'C:\\Program Files\\InventariAgent'",
                        UseShellExecute = true,
                        WorkingDirectory = extractDir
                    };

                    var configProc = Process.Start(configPsi);
                    configProc?.WaitForExit();
                    var configCode = configProc?.ExitCode ?? 0;
                    
                    if (configCode == 0)
                    {
                        Console.WriteLine("\n=== Iniciando Servicio de Windows ===");
                        // Iniciar el servicio
                        try
                        {
                            var startPsi = new ProcessStartInfo
                            {
                                FileName = "sc.exe",
                                Arguments = "start InventariAgent",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };
                            var startProc = Process.Start(startPsi);
                            startProc?.WaitForExit();
                            
                            if (startProc?.ExitCode == 0)
                            {
                                Console.WriteLine("✓ Servicio InventariAgent iniciado correctamente.");
                                Console.WriteLine("El servicio está monitoreando el equipo en segundo plano.");
                            }
                            else
                            {
                                var error = startProc?.StandardError.ReadToEnd();
                                Console.WriteLine($"⚠ No se pudo iniciar el servicio automáticamente.");
                                Console.WriteLine($"Error: {error}");
                                Console.WriteLine("Puedes iniciarlo manualmente con: sc start InventariAgent");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠ Error al intentar iniciar el servicio: {ex.Message}");
                            Console.WriteLine("Puedes iniciarlo manualmente con: sc start InventariAgent");
                        }
                    }
                    else
                    {
                        Console.WriteLine("\nNo se seleccionó dispositivo. El servicio no se iniciará automáticamente.");
                        Console.WriteLine("Ejecuta 'configure-device.ps1' desde 'C:\\Program Files\\InventariAgent' para configurarlo.");
                    }
                }
                
                Console.WriteLine("\nInstalación completada. Presiona cualquier tecla para salir...");
                Console.ReadKey();
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\nError durante la instalación: {ex.Message}");
                Console.Error.WriteLine($"\nDetalles: {ex}");
                Console.WriteLine("\nPresiona cualquier tecla para salir...");
                Console.ReadKey();
                return 1;
            }
        }
    }
}
