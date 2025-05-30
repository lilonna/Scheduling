﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class Allocation
{
    [Key]
    public int Id { get; set; }

    public int CourseId { get; set; }

    public int InstructorId { get; set; }

    public int SectionId { get; set; }

    public int ContactHour { get; set; }

    [ForeignKey("CourseId")]
    [InverseProperty("Allocations")]
    public virtual Course Course { get; set; } = null!;

    [ForeignKey("InstructorId")]
    [InverseProperty("Allocations")]
    public virtual Instructor Instructor { get; set; } = null!;

    [InverseProperty("Allocation")]
    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    [ForeignKey("SectionId")]
    [InverseProperty("Allocations")]
    public virtual Section Section { get; set; } = null!;
}
