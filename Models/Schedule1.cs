using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

[Table("Schedules")]
public partial class Schedule1
{
    [Key]
    public int Id { get; set; }

    public int AllocationId { get; set; }

    public int TimeSlotId { get; set; }

    [ForeignKey("AllocationId")]
    [InverseProperty("Schedule1s")]
    public virtual Allocation Allocation { get; set; } = null!;

    [ForeignKey("TimeSlotId")]
    [InverseProperty("Schedule1s")]
    public virtual TimeSlot TimeSlot { get; set; } = null!;
}
