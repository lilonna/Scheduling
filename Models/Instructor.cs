using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class Instructor
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string FullName { get; set; } = null!;

    [InverseProperty("Instructor")]
    public virtual ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();
}
