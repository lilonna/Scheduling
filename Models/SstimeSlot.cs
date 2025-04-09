using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

[Table("SSTimeSlots")]
public partial class SstimeSlot
{
    [Key]
    public int Id { get; set; }

    [Column("SSID")]
    public int Ssid { get; set; }

    public int CategoryId { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("SstimeSlots")]
    public virtual Category Category { get; set; } = null!;

    [ForeignKey("Ssid")]
    [InverseProperty("SstimeSlots")]
    public virtual ScheduleSetting Ss { get; set; } = null!;
}
