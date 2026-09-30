using System;
using System.Collections.Generic;

namespace Examo.Models;

public partial class Subject
{
    public int Id { get; set; }

    public string SubjectName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Materials { get; set; }

    public int Lectures { get; set; }

    public string? Pdf { get; set; }

    public string? Link { get; set; }

    public DateTime CreatedAt { get; set; }
}