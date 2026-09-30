using Examo.Models;

namespace Examo.Repositories.Interfaces;

public interface IPreparationRepository
{
    Task<IEnumerable<Preparation>> GetAllAsync();
    Task<Preparation?> GetByIdAsync(int id);
    Task<Preparation> AddAsync(Preparation preparation);
    Task<Preparation> UpdateAsync(Preparation preparation);
    Task<bool> DeleteAsync(int id);
}