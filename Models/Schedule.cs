using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class Schedule
{
    [Key]
    public int Id { get; set; }

    public int AllocationId { get; set; }

    public int TimeSlotId { get; set; }

    [ForeignKey("AllocationId")]
    [InverseProperty("Schedules")]
    public virtual Allocation Allocation { get; set; } = null!;

    [ForeignKey("TimeSlotId")]
    [InverseProperty("Schedules")]
    public virtual TimeSlot TimeSlot { get; set; } = null!;
}
