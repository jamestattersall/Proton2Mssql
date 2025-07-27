using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProtonConsole2.DataContext;

public class Attribute
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public short AttributeId { get; set; }

    public short DataTypeId { get; set; }

    public short EntityTypeId { get; set; }

    public short? TableId { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string Name { get; set; } 

    public short? Quark { get; set; }

    public short? DisplayLength { get; set; } 

    public virtual DataType? DataType { get; set; }

    public virtual Table? Table { get; set; } 

    public virtual EntityType? EntityType { get; set; } 

    public virtual ICollection<ValueDate> ValueDates { get; set; } = new List<ValueDate>();

    public virtual ICollection<ValueEntity> ValueEntities { get; set; } = new List<ValueEntity>();

    public virtual ICollection<ValueLookup> ValueLookups { get; set; } = new List<ValueLookup>();

    public virtual ICollection<ValueNumber> ValueNumbers { get; set; } = new List<ValueNumber>();

    public virtual ICollection<ValueText> ValueTexts { get; set; } = new List<ValueText>();

    public virtual ICollection<ViewAttribute> ViewAttributes { get; set; } = new List<ViewAttribute>();
}

public enum DataTypes
{
    Text=1,
    numeric=2,
    Lookup=3,
    Date=4,
    EntityPtr= 5,
    LongText = 6,
    QualifiedNumbers = 7,
    Time = 8,
}




