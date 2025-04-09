using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

[Table("SSBatchs")]
public partial class Ssbatch
{
    [Key]
    public int Id { get; set; }

    [Column("SSID")]
    public int Ssid { get; set; }

    public int BatchId { get; set; }

    [ForeignKey("Ssid")]
    [InverseProperty("Ssbatches")]
    public virtual ScheduleSetting Ss { get; set; } = null!;
}
