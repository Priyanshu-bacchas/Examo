using Examo.Services.Interfaces;

namespace Examo.Services;

// Background job: har kuch minute me check karta hai ki kaunsi reminder due hai.
//   Notifications__IntervalMinutes  default 10
// NOTE: Render free plan par server so jaata hai; tab ye job nahi chalti.
// Uske liye NotificationsController ka /cron endpoint kisi free cron se hit karo.
public class NotificationSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationSchedulerService> _logger;

    public NotificationSchedulerService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<NotificationSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // App start hone ke thodi der baad pehla run (startup halka rahe)
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var minutes = Math.Max(
            1,
            _config.GetValue("Notifications:IntervalMinutes", 10));

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(minutes),
                    stoppingToken);
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

            var service = scope.ServiceProvider
                .GetRequiredService<INotificationService>();

            await service.RunDueRemindersAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // app band ho rahi hai
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Notification scheduler run failed.");
        }
    }
}
