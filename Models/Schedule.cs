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

    [Column("SSID")]
    public int Ssid { get; set; }

    public int? RoomId { get; set; }

    [ForeignKey("AllocationId")]
    [InverseProperty("Schedules")]
    public virtual Allocation Allocation { get; set; } = null!;

    [ForeignKey("RoomId")]
    [InverseProperty("Schedules")]
    public virtual Room? Room { get; set; }

    [ForeignKey("Ssid")]
    [InverseProperty("Schedules")]
    public virtual ScheduleSetting Ss { get; set; } = null!;

    [ForeignKey("TimeSlotId")]
    [InverseProperty("Schedules")]
    public virtual TimeSlot TimeSlot { get; set; } = null!;
}
