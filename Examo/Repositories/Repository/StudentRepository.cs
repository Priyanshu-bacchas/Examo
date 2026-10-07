using Examo.Models;
using Examo.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly ExamoDbContext _context;

    public StudentRepository(ExamoDbContext context)
    {
        _context = context;
    }

    public async Task<List<Student>> GetAllAsync()
    {
        return await _context.Students
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Student?> GetByIdAsync(int id)
    {
        return await _context.Students
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Student?> GetByEmailAsync(string email)
    {
        email = email.Trim().ToLowerInvariant();

        return await _context.Students
            .FirstOrDefaultAsync(x => x.Email == email);
    }

    public async Task<Student?> GetByMobileNumberAsync(
        string mobileNumber)
    {
        mobileNumber = mobileNumber.Trim();

        return await _context.Students
            .FirstOrDefaultAsync(x => x.MobileNumber == mobileNumber);
    }

    public async Task<Student?> GetByIdentifierAsync(
        string identifier)
    {
        identifier = identifier.Trim();

        if (identifier.Contains("@"))
        {
            identifier = identifier.ToLowerInvariant();

            return await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.Email == identifier);
        }

        return await _context.Students
            .FirstOrDefaultAsync(x =>
                x.MobileNumber == identifier);
    }

    public async Task<Student> AddAsync(Student student)
    {
        await _context.Students.AddAsync(student);
        await _context.SaveChangesAsync();

        return student;
    }

    public async Task<Student> UpdateAsync(Student student)
    {
        _context.Students.Update(student);
        await _context.SaveChangesAsync();

        return student;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(x => x.Id == id);

        if (student == null)
            return false;

        _context.Students.Remove(student);

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> EmailExistsAsync(
        string email,
        int? excludeId = null)
    {
        email = email.Trim().ToLowerInvariant();

        var query = _context.Students
            .Where(x => x.Email == email);

        if (excludeId.HasValue)
        {
            query = query.Where(
                x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<bool> MobileExistsAsync(
        string mobileNumber,
        int? excludeId = null)
    {
        mobileNumber = mobileNumber.Trim();

        var query = _context.Students
            .Where(x => x.MobileNumber == mobileNumber);

        if (excludeId.HasValue)
        {
            query = query.Where(
                x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}