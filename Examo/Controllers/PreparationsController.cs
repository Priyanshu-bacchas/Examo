using Examo.DTOs.Preparation;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PreparationsController : ControllerBase
{
    private readonly IPreparationService _service;

    public PreparationsController(IPreparationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var preparations = await _service.GetAllAsync();

        return Ok(preparations);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var preparation = await _service.GetByIdAsync(id);

        if (preparation == null)
        {
            return NotFound(new
            {
                Message = "Preparation not found."
            });
        }

        return Ok(preparation);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        PreparationCreateDto dto)
    {
        var preparation = await _service.AddAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = preparation.Id },
            preparation);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        PreparationUpdateDto dto)
    {
        var preparation = await _service.UpdateAsync(id, dto);

        if (preparation == null)
        {
            return NotFound(new
            {
                Message = "Preparation not found."
            });
        }

        return Ok(preparation);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
        {
            return NotFound(new
            {
                Message = "Preparation not found."
            });
        }

        return NoContent();
    }
}