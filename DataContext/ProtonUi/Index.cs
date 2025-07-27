﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ProtonConsole2.DataContext;

[PrimaryKey(nameof(IndexTypeId), nameof(Term), nameof(EntityId))]
[Index(nameof(IndexTypeId), nameof(EntityId))]
public partial class Index
{
    public short IndexTypeId { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string Term { get; set; }

   
    public int EntityId { get; set; }

}   
