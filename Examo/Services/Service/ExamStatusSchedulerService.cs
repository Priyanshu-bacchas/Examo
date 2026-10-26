using Microsoft.EntityFrameworkCore;

using Examo.Models;

namespace Examo.Services;

// Background job: jin Exams ki date nikal chuki hai (aur status Done nahi hai)
// unka status database me "Done" kar deta hai.
// Exams page kholne par bhi yahi rule chalta hai (ExamService.GetAllAsync).
public class ExamStatusSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExamStatusSchedulerService> _logger;

    public ExamStatusSchedulerService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExamStatusSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ExamoDbContext>();

            var today = ExamStatusHelper.Today();

            await db.Exams
                .Where(x =>
                    x.ExamDate != null &&
                    x.ExamDate < today &&
                    x.Status != ExamStatusHelper.Done)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(
                        x => x.Status,
                        ExamStatusHelper.Done),
                    stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Exam status auto-update failed.");
        }
    }
}
