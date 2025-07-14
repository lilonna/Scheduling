using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

[Table("DaysOfWeek")]
public partial class DaysOfWeek
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [InverseProperty("DayOfWeek")]
    public virtual ICollection<AdmissionTypeDay> AdmissionTypeDays { get; set; } = new List<AdmissionTypeDay>();

    [InverseProperty("DaysOfWeek")]
    public virtual ICollection<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();
}
