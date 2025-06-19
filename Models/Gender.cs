using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

[Table("Gender")]
public partial class Gender
{
    [Key]
    public int Id { get; set; }

    [StringLength(20)]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    [InverseProperty("Gender")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
