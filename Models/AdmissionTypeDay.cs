using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class AdmissionTypeDay
{
    [Key]
    public int Id { get; set; }

    public int AdmissionTypeId { get; set; }

    public int DayOfWeekId { get; set; }

    [ForeignKey("AdmissionTypeId")]
    [InverseProperty("AdmissionTypeDays")]
    public virtual AdmissionType AdmissionType { get; set; } = null!;

    [ForeignKey("DayOfWeekId")]
    [InverseProperty("AdmissionTypeDays")]
    public virtual DaysOfWeek DayOfWeek { get; set; } = null!;
}
