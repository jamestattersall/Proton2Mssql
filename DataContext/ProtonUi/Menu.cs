using ProtonConsole2.DataContext.ProtonUi;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProtonConsole2.DataContext;

public partial class Menu
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public short Id { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string Name { get; set; }

    public short EntityTypeId { get; set; }   

    public virtual EntityType EntityType { get; set; }

}
