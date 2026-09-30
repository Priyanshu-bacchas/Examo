using Examo.DTOs.Schedule;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchedulesController : ControllerBase
{
    private readonly IScheduleService _service;

public SchedulesController(IScheduleService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var schedules = await _service.GetAllAsync();
        return Ok(schedules);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var schedule = await _service.GetByIdAsync(id);

        if (schedule == null)
        {
            return NotFound(new
            {
                Message = "Schedule not found."
            });
        }

        return Ok(schedule);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ScheduleCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (dto.EndTime.HasValue &&
            dto.EndTime.Value < dto.StartTime)
        {
            return BadRequest(new
            {
                Message = "End time cannot be earlier than start time."
            });
        }

        var schedule = await _service.AddAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = schedule.Id },
            schedule
        );
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] ScheduleUpdateDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (dto.EndTime.HasValue &&
            dto.EndTime.Value < dto.StartTime)
        {
            return BadRequest(new
            {
                Message = "End time cannot be earlier than start time."
            });
        }

        var schedule = await _service.UpdateAsync(id, dto);

        if (schedule == null)
        {
            return NotFound(new
            {
                Message = "Schedule not found."
            });
        }

        return Ok(schedule);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
        {
            return NotFound(new
            {
                Message = "Schedule not found."
            });
        }

        return NoContent();
    }


}
