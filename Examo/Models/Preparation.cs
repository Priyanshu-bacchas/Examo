using System;
using System.Collections.Generic;

namespace Examo.Models;

public partial class Preparation
{
    public int Id { get; set; }

    public string ExamName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
