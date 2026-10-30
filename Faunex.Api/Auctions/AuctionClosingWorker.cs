using Faunex.Application.DTOs;
using Faunex.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Auctions;

public sealed class AuctionClosingWorker(IServiceScopeFactory scopes, ILogger<AuctionClosingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var read = scopes.CreateScope();
                var ids = await read.ServiceProvider.GetRequiredService<AuctionSettlement>().DueAsync(stoppingToken);
                foreach (var id in ids)
                {
                    using var write = scopes.CreateScope();
                    try { await write.ServiceProvider.GetRequiredService<AuctionSettlement>().SettleAsync(id, stoppingToken); }
                    catch (Exception ex) when (ex is DbUpdateConcurrencyException or ListingEditConflictException) { }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogError(ex, "Auction settlement failed for {AuctionId}", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Auction settlement sweep failed"); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
