using Examo.DTOs.Student;
using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;

namespace Examo.Services;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _repository;

    public StudentService(IStudentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<Student>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<Student?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<Student> AddAsync(StudentCreateDto dto)
    {
        var student = new Student
        {
            Name = dto.Name,
            Email = dto.Email,
            Course = dto.Course,
            Age = dto.Age,
            City = dto.City
        };

        return await _repository.AddAsync(student);
    }

    public async Task<Student?> UpdateAsync(
        int id,
        StudentUpdateDto dto)
    {
        var student = await _repository.GetByIdAsync(id);

        if (student == null)
        {
            return null;
        }

        student.Name = dto.Name;
        student.Email = dto.Email;
        student.Course = dto.Course;
        student.Age = dto.Age;
        student.City = dto.City;

        return await _repository.UpdateAsync(student);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }
}