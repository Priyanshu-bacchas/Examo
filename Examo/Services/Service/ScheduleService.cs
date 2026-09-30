using Examo.DTOs.Schedule;
using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;

namespace Examo.Services;

public class ScheduleService : IScheduleService
{
    private readonly IScheduleRepository _repository;

    public ScheduleService(IScheduleRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<Schedule>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<Schedule?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<Schedule> AddAsync(
        ScheduleCreateDto dto)
    {
        var schedule = new Schedule
        {
            Subject = dto.Subject,
            ScheduleDate = dto.ScheduleDate,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Description = dto.Description,
            Lecture = dto.Lecture
        };

        return await _repository.AddAsync(schedule);
    }

    public async Task<Schedule?> UpdateAsync(
        int id,
        ScheduleUpdateDto dto)
    {
        var schedule = await _repository.GetByIdAsync(id);

        if (schedule == null)
        {
            return null;
        }

        schedule.Subject = dto.Subject;
        schedule.ScheduleDate = dto.ScheduleDate;
        schedule.StartTime = dto.StartTime;
        schedule.EndTime = dto.EndTime;
        schedule.Description = dto.Description;
        schedule.Lecture = dto.Lecture;

        return await _repository.UpdateAsync(schedule);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }
}