using System.Diagnostics;

using Examo.Models;
using Examo.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Examo.Controllers;

// System health: Web Service (uptime, CPU, memory) + Database - sirf Admin
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class SystemMetricsController : ControllerBase
{
    private readonly ExamoDbContext _db;
    private readonly SystemMetricsService _metrics;

    public SystemMetricsController(
        ExamoDbContext db,
        SystemMetricsService metrics)
    {
        _db = db;
        _metrics = metrics;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var database = await ReadDatabaseAsync();

        if (database.Connected && database.LatencyMs.HasValue)
        {
            _metrics.RecordLatency(database.LatencyMs.Value);
        }

        var snapshot = _metrics.GetSnapshot();

        var uptime = DateTime.UtcNow - _metrics.StartedAtUtc;

        return Ok(new
        {
            intervalSeconds = _metrics.IntervalSecondsValue,

            web = new
            {
                status = "Live",
                startedAtUtc = _metrics.StartedAtUtc,
                uptimeSeconds = (long)uptime.TotalSeconds,
                cpuPercent = snapshot.CpuPercent,
                cpuHistory = snapshot.CpuHistory,
                memoryMb = snapshot.MemoryMb,
                memoryPercent = snapshot.MemoryPercent,
                memoryHistory = snapshot.MemoryHistory,
                cores = Environment.ProcessorCount
            },

            database = new
            {
                connected = database.Connected,
                status = database.Connected
                    ? "Connected"
                    : "Disconnected",
                latencyMs = database.LatencyMs,
                latencyHistory = _metrics.GetSnapshot().LatencyHistory,
                activeConnections = database.Active,
                maxConnections = database.Max
            }
        });
    }

    private record DatabaseInfo(
        bool Connected,
        double? LatencyMs,
        int? Active,
        int? Max);

    private async Task<DatabaseInfo> ReadDatabaseAsync()
    {
        try
        {
            var connection = _db.Database.GetDbConnection();

            await _db.Database.OpenConnectionAsync();

            try
            {
                // 1) Connected? + query ka response time
                using var ping = connection.CreateCommand();
                ping.CommandText = "SELECT 1";

                var watch = Stopwatch.StartNew();
                await ping.ExecuteScalarAsync();
                watch.Stop();

                var latency = Math.Round(
                    watch.Elapsed.TotalMilliseconds,
                    1);

                // 2) Active connections / max connections
                int? active = null;
                int? max = null;

                try
                {
                    using var command = connection.CreateCommand();

                    command.CommandText =
                        "SELECT " +
                        "(SELECT count(*) FROM pg_stat_activity " +
                        " WHERE datname = current_database())::int, " +
                        "(SELECT setting::int FROM pg_settings " +
                        " WHERE name = 'max_connections')";

                    using var reader =
                        await command.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        active = reader.GetInt32(0);
                        max = reader.GetInt32(1);
                    }
                }
                catch
                {
                    // permission na ho to sirf ye numbers khali rahenge
                }

                return new DatabaseInfo(true, latency, active, max);
            }
            finally
            {
                await _db.Database.CloseConnectionAsync();
            }
        }
        catch
        {
            return new DatabaseInfo(false, null, null, null);
        }
    }
}
