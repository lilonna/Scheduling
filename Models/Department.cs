using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class Department
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [InverseProperty("Department")]
    public virtual ICollection<DepartmentAdmin> DepartmentAdmins { get; set; } = new List<DepartmentAdmin>();

    [InverseProperty("Department")]
    public virtual ICollection<ScheduleSetting> ScheduleSettings { get; set; } = new List<ScheduleSetting>();

    [InverseProperty("Department")]
    public virtual ICollection<Section> Sections { get; set; } = new List<Section>();
}
