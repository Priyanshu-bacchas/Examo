using Examo.DTOs.Schedule;
using Examo.Models;

namespace Examo.Services.Interfaces;

public interface IScheduleService
{
    Task<IEnumerable<Schedule>> GetAllAsync();
    Task<Schedule?> GetByIdAsync(int id);
    Task<Schedule> AddAsync(ScheduleCreateDto dto);
    Task<Schedule?> UpdateAsync(int id, ScheduleUpdateDto dto);
    Task<bool> DeleteAsync(int id);
}