using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

[Keyless]
public partial class Schedule
{
    public int Id { get; set; }

    public int AllocationId { get; set; }

    public int TimeSlotId { get; set; }
}
