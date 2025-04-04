using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

[Keyless]
[Table("Table_1")]
public partial class Table1
{
    public int Id { get; set; }
}
