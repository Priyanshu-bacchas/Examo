using Examo.Models;

namespace Examo.Repositories.Interfaces;

public interface IScheduleRepository
{
    Task<IEnumerable<Schedule>> GetAllAsync();
    Task<Schedule?> GetByIdAsync(int id);
    Task<Schedule> AddAsync(Schedule schedule);
    Task<Schedule> UpdateAsync(Schedule schedule);
    Task<bool> DeleteAsync(int id);
}