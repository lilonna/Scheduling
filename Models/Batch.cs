using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class Batch
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    public int AdmissionTypesId { get; set; }

    public int ProgramsId { get; set; }

    [ForeignKey("AdmissionTypesId")]
    [InverseProperty("Batches")]
    public virtual AdmissionType AdmissionTypes { get; set; } = null!;

    [ForeignKey("ProgramsId")]
    [InverseProperty("Batches")]
    public virtual Program Programs { get; set; } = null!;

    [InverseProperty("Batch")]
    public virtual ICollection<Section> Sections { get; set; } = new List<Section>();
}
