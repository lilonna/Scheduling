using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class DepartmentAdmin
{
    [Key]
    public int Id { get; set; }

    public int DepartmentId { get; set; }

    public int UserId { get; set; }

    [ForeignKey("DepartmentId")]
    [InverseProperty("DepartmentAdmins")]
    public virtual Department Department { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("DepartmentAdmins")]
    public virtual User User { get; set; } = null!;
}
