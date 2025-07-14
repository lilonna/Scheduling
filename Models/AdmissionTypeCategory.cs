using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class AdmissionTypeCategory
{
    [Key]
    public int Id { get; set; }

    public int AdmissionTypeId { get; set; }

    public int CategoryId { get; set; }

    [ForeignKey("AdmissionTypeId")]
    [InverseProperty("AdmissionTypeCategories")]
    public virtual AdmissionType AdmissionType { get; set; } = null!;

    [ForeignKey("CategoryId")]
    [InverseProperty("AdmissionTypeCategories")]
    public virtual Category Category { get; set; } = null!;
}
