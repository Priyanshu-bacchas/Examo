using System;
using System.Collections.Generic;

namespace Examo.Models;

public partial class Student
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Course { get; set; }

    public int? Age { get; set; }

    public string? City { get; set; }

    public DateTime CreatedAt { get; set; }
}
