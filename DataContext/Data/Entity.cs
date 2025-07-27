using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ProtonConsole2.DataContext;

public partial class Entity
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int EntityId { get; set; }

    public short EntityTypeId { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string Name { get; set; } = string.Empty;

    public DateTime LastUpdated { get; set; }


    public virtual ICollection<Index> Indices { get; set; } = new List<Index>();

    public virtual ICollection<ValueDate> ValueDates { get; set; } = new List<ValueDate>();
    public virtual ICollection<ValueEntity> ValueEntities { get; set; } = new List<ValueEntity>();
    public virtual ICollection<ValueLookup> ValueLookups { get; set; } = new List<ValueLookup>();
    public virtual ICollection<ValueNumber> ValueNumbers { get; set; } = new List<ValueNumber>();
    public virtual ICollection<ValueText> ValueTexts { get; set; } = new List<ValueText>();
    public virtual ICollection<ValueLongText> ValueLongTexts { get; set; } = new List<ValueLongText>();
    public virtual ICollection<ValueTime> ValueTimes { get; set; } = new List<ValueTime>();

}
