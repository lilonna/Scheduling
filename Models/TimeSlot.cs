using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

[Keyless]
public partial class TimeSlot
{
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    public int DaysOfWeekId { get; set; }

    [StringLength(50)]
    public string From { get; set; } = null!;

    [StringLength(50)]
    public string To { get; set; } = null!;

    public int CategoryId { get; set; }

    [ForeignKey("CategoryId")]
    public virtual Category Category { get; set; } = null!;

    [ForeignKey("DaysOfWeekId")]
    public virtual DaysOfWeek DaysOfWeek { get; set; } = null!;
}
