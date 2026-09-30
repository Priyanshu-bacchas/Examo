using Examo.DTOs.Preparation;
using Examo.Models;

namespace Examo.Services.Interfaces;

public interface IPreparationService
{
    Task<IEnumerable<Preparation>> GetAllAsync();
    Task<Preparation?> GetByIdAsync(int id);
    Task<Preparation> AddAsync(PreparationCreateDto dto);
    Task<Preparation?> UpdateAsync(int id, PreparationUpdateDto dto);
    Task<bool> DeleteAsync(int id);
}