﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class Category
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [InverseProperty("Category")]
    public virtual ICollection<AdmissionTypeCategory> AdmissionTypeCategories { get; set; } = new List<AdmissionTypeCategory>();

    [InverseProperty("Category")]
    public virtual ICollection<SstimeSlot> SstimeSlots { get; set; } = new List<SstimeSlot>();

    [InverseProperty("Category")]
    public virtual ICollection<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();
}
