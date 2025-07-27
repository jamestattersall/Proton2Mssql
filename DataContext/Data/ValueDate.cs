using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProtonConsole2.DataContext;

[PrimaryKey(nameof(EntityId), nameof(AttributeId), nameof(Seq))]
public abstract class Value(int entityId, short attributeId, short seq)
{
    public int EntityId { get; set; } = entityId;

    public short AttributeId { get; set; } = attributeId;

    //row ordinal number
    public short Seq { get; set; } = seq;

    public virtual Entity Entity { get; set; } = null!;
}

[Index(nameof(Value), AllDescending = true)]
public partial class ValueDate(int entityId, short attributeId, short seq) : Value( entityId,  attributeId,  seq)
{
    public DateOnly Value { get; set; }
}

