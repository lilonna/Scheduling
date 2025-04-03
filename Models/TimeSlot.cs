using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class TimeSlot
{
    [Key]
    public int Id { get; set; }

    public int DayaOfWeekId { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    public int AllocationId { get; set; }

    [StringLength(50)]
    public string FromTo { get; set; } = null!;

    [ForeignKey("AllocationId")]
    [InverseProperty("TimeSlots")]
    public virtual Allocation Allocation { get; set; } = null!;

    [ForeignKey("DayaOfWeekId")]
    [InverseProperty("TimeSlots")]
    public virtual DaysOfWeek DayaOfWeek { get; set; } = null!;
}
