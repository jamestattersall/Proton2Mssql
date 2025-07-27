using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ProtonConsole2.DataContext;

public partial class View
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public short ViewId { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string Name { get; set; } 

    public short NRows { get; set; }

    public short NItems { get; set; }

    public short TableId { get; set; }

    public short EntityTypeId { get; set; }

    public virtual Table? Table { get; set; }

    public virtual EntityType? EntityType { get; set; }

    public virtual ICollection<ViewAttribute> ViewAttributes { get; set; } = new List<ViewAttribute>();

    public virtual ICollection<ViewCaption> ViewCaptions { get; set; } = new List<ViewCaption>();
}
