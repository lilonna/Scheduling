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

    public int BatchId { get; set; }

    public int DepartmentId { get; set; }

    public int? RoomId { get; set; }

    [InverseProperty("Section")]
    public virtual ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();

    [ForeignKey("BatchId")]
    [InverseProperty("Sections")]
    public virtual Batch Batch { get; set; } = null!;

    [ForeignKey("DepartmentId")]
    [InverseProperty("Sections")]
    public virtual Department Department { get; set; } = null!;

    [ForeignKey("RoomId")]
    [InverseProperty("Sections")]
    public virtual Room? Room { get; set; }
}
