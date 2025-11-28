using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InventariAgentSvc.Services;

public class IncidentMailSender
{
    private readonly ILogger<IncidentMailSender> _logger;
    private readonly FirebaseClient _firebaseClient;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _endpoint;
    private readonly string _apiKey;

    public IncidentMailSender(
        ILogger<IncidentMailSender> logger,
        FirebaseClient firebaseClient,
        IConfiguration configuration)
    {
        _logger = logger;
        _firebaseClient = firebaseClient;
        _httpClient = new HttpClient();

        var smtpSection = configuration.GetSection("SmtpApi");
        _baseUrl = smtpSection["BaseUrl"] ?? "https://gery.myqnapcloud.com:4000";
        _endpoint = smtpSection["Endpoint"] ?? "/api/sendMail";
        _apiKey = smtpSection["ApiKey"] ?? "";

        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("SMTP API Key no configurada. El envío de correos no funcionará.");
        }
    }

    public async Task SendIncidentMailAsync(string deviceId, string category, string description, string priority)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("⚠️ SMTP API Key no configurada. No se enviará correo.");
            return;
        }

        try
        {
            _logger.LogInformation("📧 Iniciando proceso de envío de correo para incidencia: {Desc}", description);

            // 1. Obtener correos de admins
            var adminEmails = await _firebaseClient.GetAdminEmailsAsync();
            if (adminEmails == null || adminEmails.Count == 0)
            {
                _logger.LogWarning("⚠️ No se encontraron administradores para enviar correo de incidencia.");
                return;
            }
            
            _logger.LogInformation("👥 Encontrados {Count} administradores: {Emails}", adminEmails.Count, string.Join(", ", adminEmails));

            // 2. Construir contenido
            var subject = $"🚨 Nova Incidència: {description}";
            var htmlContent = GenerateHtmlTemplate(deviceId, category, description, priority);

            // 3. Enviar a cada admin
            foreach (var email in adminEmails)
            {
                await SendSingleMailAsync(email, subject, htmlContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error CRÍTICO en el proceso de envío de correos de incidencia.");
        }
    }

    private async Task SendSingleMailAsync(string to, string subject, string html)
    {
        try
        {
            var url = $"{_baseUrl}{_endpoint}";
            _logger.LogInformation("📤 Enviando a {Email} vía {Url}...", to, url);
            
            var payload = new
            {
                to = to,
                subject = subject,
                html = html
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-api-key", _apiKey);
            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("❌ Fallo al enviar correo a {Email}. Status: {Status}. Error: {Error}", to, response.StatusCode, error);
            }
            else
            {
                _logger.LogInformation("✅ Correo de incidencia enviado correctamente a {Email}", to);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Excepción enviando correo a {Email}", to);
        }
    }

    private string GenerateHtmlTemplate(string deviceId, string category, string description, string priority)
    {
        var date = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        var color = priority.ToLower() switch
        {
            "high" => "#ef4444",
            "medium" => "#f59e0b",
            "low" => "#3b82f6",
            _ => "#6b7280"
        };

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #374151; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #1f2937; color: white; padding: 20px; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #ffffff; padding: 20px; border: 1px solid #e5e7eb; border-radius: 0 0 8px 8px; }}
        .badge {{ display: inline-block; padding: 4px 12px; border-radius: 9999px; font-size: 12px; font-weight: 600; color: white; background-color: {color}; }}
        .field {{ margin-bottom: 16px; }}
        .label {{ font-size: 12px; color: #6b7280; text-transform: uppercase; letter-spacing: 0.05em; font-weight: 600; }}
        .value {{ font-size: 16px; font-weight: 500; margin-top: 4px; }}
        .button {{ display: inline-block; background-color: #2563eb; color: white; padding: 12px 24px; border-radius: 6px; text-decoration: none; font-weight: 500; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1 style=""margin:0; font-size: 24px;"">Nova Incidència Detectada</h1>
        </div>
        <div class=""content"">
            <div class=""field"">
                <div class=""label"">Títol</div>
                <div class=""value"">{description}</div>
            </div>
            
            <div class=""field"">
                <div class=""label"">Equip</div>
                <div class=""value"">{deviceId}</div>
            </div>

            <div class=""field"">
                <div class=""label"">Prioritat</div>
                <div style=""margin-top:4px;"">
                    <span class=""badge"">{priority.ToUpper()}</span>
                </div>
            </div>

            <div class=""field"">
                <div class=""label"">Categoria</div>
                <div class=""value"">{category}</div>
            </div>

            <div class=""field"">
                <div class=""label"">Data</div>
                <div class=""value"">{date}</div>
            </div>
            <div class=""field"">
                <div class=""label"">Estat</div>
                <div class=""value"">Oberta (Automàtica)</div>
            </div>

            <a href=""https://laferreria-inventari.web.app/pcs/{deviceId}"" class=""button"">
                Veure Equip
            </a>
        </div>
    </div>
</body>
</html>";
    }
}
