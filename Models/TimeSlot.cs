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

    [StringLength(50)]
    public string Name { get; set; } = null!;

    public int DaysOfWeekId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime From { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime To { get; set; }

    public int CategoryId { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("TimeSlots")]
    public virtual Category Category { get; set; } = null!;

    [ForeignKey("DaysOfWeekId")]
    [InverseProperty("TimeSlots")]
    public virtual DaysOfWeek DaysOfWeek { get; set; } = null!;

    [InverseProperty("TimeSlot")]
    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
