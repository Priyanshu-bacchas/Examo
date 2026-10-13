using System.Diagnostics;

namespace Examo.Services;

// Web service ki CPU / Memory ko har 5 second sample karke last 5 minute ki
// history memory me rakhta hai. Database ka koi load nahi.
public class SystemMetricsService : BackgroundService
{
    private const int MaxPoints = 60;
    private const int IntervalSeconds = 5;

    private readonly object _lock = new();

    private readonly Queue<double> _cpu = new();
    private readonly Queue<double> _memory = new();
    private readonly Queue<double> _latency = new();

    private DateTime _lastLatencyUtc = DateTime.MinValue;

    private double _cpuPercent;
    private double _memoryMb;
    private double _memoryPercent;

    public DateTime StartedAtUtc { get; }

    public int IntervalSecondsValue => IntervalSeconds;

    public SystemMetricsService()
    {
        using var process = Process.GetCurrentProcess();

        StartedAtUtc = process.StartTime.ToUniversalTime();
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var process = Process.GetCurrentProcess();

        var lastCpuTime = process.TotalProcessorTime;
        var lastSampleAt = DateTime.UtcNow;

        // Pehla point turant, taaki chart khali na rahe
        Record(process, 0);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(IntervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                process.Refresh();

                var now = DateTime.UtcNow;
                var cpuTime = process.TotalProcessorTime;

                var elapsedMs = (now - lastSampleAt).TotalMilliseconds;
                var usedMs = (cpuTime - lastCpuTime).TotalMilliseconds;

                var cpu = elapsedMs > 0
                    ? usedMs / (elapsedMs * Environment.ProcessorCount) * 100
                    : 0;

                lastCpuTime = cpuTime;
                lastSampleAt = now;

                Record(process, Math.Clamp(cpu, 0, 100));
            }
        }
        catch (OperationCanceledException)
        {
            // app band ho rahi hai
        }
    }

    private void Record(Process process, double cpuPercent)
    {
        var workingSet = process.WorkingSet64;

        // Container ki memory limit ka dhyan rakhta hai (Render etc.)
        var totalBytes =
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        var memoryPercent = totalBytes > 0
            ? Math.Clamp(workingSet * 100.0 / totalBytes, 0, 100)
            : 0;

        lock (_lock)
        {
            _cpuPercent = cpuPercent;
            _memoryMb = workingSet / 1024.0 / 1024.0;
            _memoryPercent = memoryPercent;

            Push(_cpu, Math.Round(cpuPercent, 1));
            Push(_memory, Math.Round(memoryPercent, 1));
        }
    }

    // Database ping ka time (ms); kam se kam 4 second ke gap par save hota hai
    public void RecordLatency(double milliseconds)
    {
        lock (_lock)
        {
            if ((DateTime.UtcNow - _lastLatencyUtc).TotalSeconds < 4)
                return;

            _lastLatencyUtc = DateTime.UtcNow;

            Push(_latency, Math.Round(milliseconds, 1));
        }
    }

    public MetricsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new MetricsSnapshot(
                Math.Round(_cpuPercent, 1),
                Math.Round(_memoryMb, 0),
                Math.Round(_memoryPercent, 1),
                _cpu.ToArray(),
                _memory.ToArray(),
                _latency.ToArray());
        }
    }

    private static void Push(Queue<double> queue, double value)
    {
        queue.Enqueue(value);

        while (queue.Count > MaxPoints)
            queue.Dequeue();
    }
}

public record MetricsSnapshot(
    double CpuPercent,
    double MemoryMb,
    double MemoryPercent,
    double[] CpuHistory,
    double[] MemoryHistory,
    double[] LatencyHistory);
