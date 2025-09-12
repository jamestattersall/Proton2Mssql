using ProtonConsole2.DataContext.ProtonUi;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace ProtonConsole2.DataContext;

public partial class IndexKey
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public short Id { get; set; }

    public short EntityTypeId { get; set; }

    public short IndexTypeId { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string Name { get; set; }

    public string? Prefix { get; set; } = null;

    public int IdLineViewId { get; set; }

    [ForeignKey(nameof(IdLineViewId))]
    public virtual View View { get; set; }


}
