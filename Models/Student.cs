using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class Student
{
    [StringLength(50)]
    public string Name { get; set; } = null!;

    public int? UserId { get; set; }

    public int? BatchId { get; set; }

    public int? SectionId { get; set; }

    [Key]
    public int Id { get; set; }
}
