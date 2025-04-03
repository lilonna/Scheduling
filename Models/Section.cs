using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class Section
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    public int Batch { get; set; }

    [StringLength(50)]
    public string DepartmentId { get; set; } = null!;

    [InverseProperty("Section")]
    public virtual ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();
}
