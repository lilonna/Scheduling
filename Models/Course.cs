﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class Course
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    public int CreditHour { get; set; }

    [InverseProperty("Course")]
    public virtual ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();
}
