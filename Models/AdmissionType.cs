using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class AdmissionType
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [InverseProperty("AdmissionType")]
    public virtual ICollection<AdmissionTypeCategory> AdmissionTypeCategories { get; set; } = new List<AdmissionTypeCategory>();

    [InverseProperty("AdmissionType")]
    public virtual ICollection<AdmissionTypeDay> AdmissionTypeDays { get; set; } = new List<AdmissionTypeDay>();

    [InverseProperty("AdmissionTypes")]
    public virtual ICollection<Batch> Batches { get; set; } = new List<Batch>();
}
