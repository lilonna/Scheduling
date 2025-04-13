using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class ScheduleSetting
{
    [Key]
    public int Id { get; set; }

    public int DepartmentId { get; set; }

    [StringLength(50)]
    public string Title { get; set; } = null!;

    [ForeignKey("DepartmentId")]
    [InverseProperty("ScheduleSettings")]
    public virtual Department Department { get; set; } = null!;

    [InverseProperty("Ss")]
    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    [InverseProperty("Ss")]
    public virtual ICollection<Ssbatch> Ssbatches { get; set; } = new List<Ssbatch>();

    [InverseProperty("Ss")]
    public virtual ICollection<SstimeSlot> SstimeSlots { get; set; } = new List<SstimeSlot>();
}
