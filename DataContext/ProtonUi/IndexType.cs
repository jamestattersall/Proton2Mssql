using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProtonConsole2.DataContext;

public partial class IndexType 
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public short IndexTypeId { get; set; } 

    public short EntityTypeId { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string Name { get; set; } 

    public string? Prefix { get; set; } = null;

    public short IdLineViewId { get; set; }

    [NotMapped]
    public short KeyLength { get; set; }   //required to read Index.dbs , not for SQ

    [NotMapped]
    public int StartIndexId { get; set; } // "

    [NotMapped]
    public int MiddleIndexId { get; set; }// 


    [NotMapped]
    public int AttributeId1 { get; set; }// 


    [NotMapped]
    public int? AttributeId2 { get; set; }// 

    [ForeignKey(nameof(IdLineViewId))]
    public virtual View? IdLineView { get; set; } = null;

    public virtual EntityType? EntityType { get; set; } = null;

    public virtual ICollection<Index> Indices { get; set; } = [];
}
