using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlayerServices.Services;

internal static class WebhookService
{
    private const int TIMEOUT_SECONDS = 10;
    private static readonly HttpClient _http = new();
    public static bool IsEnabled => !string.IsNullOrWhiteSpace(Plugin.changeNameWebhookUrl?.Value);
    
    public static async Task<(bool ok, string error)> SendAsync(string message, CancellationToken ct = default)
    {
        try
        {
            if (!IsEnabled)
                return (false, "Webhook URL is empty or disabled.");

            string url = Plugin.changeNameWebhookUrl.Value;

            message = (message ?? "").Trim();
            if (message.Length == 0)
                return (false, "Message is empty.");

            if (message.Length > 1990)
                message = message[..1990] + "...";

            var payload = new
            {
                content = message,
                allowed_mentions = new { parse = Array.Empty<string>() }
            };

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            var body = JsonSerializer.Serialize(payload);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url, content, linkedCts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return (false, $"Discord returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseText}");
            }

            return (true, null);
        }

        // -------------------- Hide Error Log During AutoSave --------------------
        catch (OperationCanceledException) 
        {
            Core.Log.LogInfo("[Webhook] Request timeout. The message should already be in Discord.");
            return (true, "Timeout ignored");
        }
        // ------------------------------------------------------------------------

        catch (Exception e)
        {
            Core.LogException(e);
            return (false, e.Message);
        }
    }
}
