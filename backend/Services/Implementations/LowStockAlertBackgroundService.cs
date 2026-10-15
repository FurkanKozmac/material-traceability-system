using System.Net;
using backend.Common.Events;
using backend.Data;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public sealed class LowStockAlertBackgroundService(
    ILowStockChannel channel,
    IServiceScopeFactory scopeFactory,
    ILogger<LowStockAlertBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var alert in channel.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<MtsDbContext>();
                var settings = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, stoppingToken);
                if (settings is null || !settings.EnableLowStockAlerts) continue;

                var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await email.SendEmailAsync(settings.AlertEmailRecipient,
                    $"Kritik stok uyarısı: {alert.ChemicalName}",
                    BuildHtml(alert), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Düşük stok e-postası gönderilemedi. Kimyasal {ChemicalId}", alert.ChemicalId);
            }
        }
    }

    private static string BuildHtml(LowStockAlertEvent alert)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);
        var badgeColor = alert.RemainingStock == 0 ? "#b42318" : "#c2410c";
        var status = alert.RemainingStock == 0 ? "STOK TÜKENDİ" : "KRİTİK STOK";
        return $"""<!doctype html><html><body style="margin:0;background:#f1f5f9;font-family:Arial,sans-serif;color:#172033"><div style="max-width:620px;margin:32px auto;background:#fff;border:1px solid #dbe2ea;border-radius:12px;overflow:hidden"><div style="padding:24px 28px;background:#142235;color:#fff"><div style="font-size:13px;letter-spacing:1px;color:#cbd5e1">MTS · DEPO BİLDİRİMİ</div><h1 style="font-size:22px;margin:12px 0 0">Stok seviyesi kritik</h1></div><div style="padding:28px"><span style="display:inline-block;background:{badgeColor};color:#fff;padding:7px 12px;border-radius:20px;font-size:12px;font-weight:bold">{status}</span><p style="font-size:18px;font-weight:bold;margin:22px 0 8px">{E(alert.ChemicalName)}</p><p style="margin:0 0 20px;color:#64748b">Kimyasal kodu: {E(alert.ChemicalCode)}</p><table style="width:100%;border-collapse:collapse"><tr><td style="padding:12px;border-top:1px solid #e2e8f0;color:#64748b">Parti / Lot</td><td style="padding:12px;border-top:1px solid #e2e8f0;text-align:right;font-weight:bold">{E(alert.BatchNumber)}</td></tr><tr><td style="padding:12px;border-top:1px solid #e2e8f0;color:#64748b">Kalan aktif stok</td><td style="padding:12px;border-top:1px solid #e2e8f0;text-align:right;font-size:20px;font-weight:bold;color:{badgeColor}">{alert.RemainingStock}</td></tr><tr><td style="padding:12px;border-top:1px solid #e2e8f0;color:#64748b">Kritik eşik</td><td style="padding:12px;border-top:1px solid #e2e8f0;text-align:right">{alert.MinThreshold}</td></tr></table><p style="margin:24px 0 0;color:#64748b;font-size:13px">Zaman: {alert.Timestamp.ToUniversalTime():yyyy-MM-dd HH:mm} UTC</p></div><div style="padding:16px 28px;background:#f8fafc;color:#64748b;font-size:12px">Bu otomatik bir MTS depo uyarısıdır.</div></div></body></html>""";
    }
}
