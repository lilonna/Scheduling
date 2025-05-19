using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class Room
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string RoomNumber { get; set; } = null!;

    public int BlockId { get; set; }

    [ForeignKey("BlockId")]
    [InverseProperty("Rooms")]
    public virtual Block Block { get; set; } = null!;

    [InverseProperty("Room")]
    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    [InverseProperty("Room")]
    public virtual ICollection<Section> Sections { get; set; } = new List<Section>();
}
