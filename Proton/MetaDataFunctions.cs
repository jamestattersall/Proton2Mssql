
using ProtonConsole2.DataContext;
using ProtonConsole2.DataContext.ProtonUi;

namespace ProtonConsole2.Proton
{
    public class MetaDataFunctions
    {
        public static List<DataContext.EntityType> GetEntityTypes()
        {
            List<DataContext.EntityType> list = [];
            using Proton.EntityDef entityDef = new();
            for (short ix = 1; ix <= entityDef.NPages; ix++)
            {
                if (entityDef.MoveToPage(ix))
                {
                    list.Add(new()
                    {
                        Id = ix,
                        Name = entityDef.Name,
                        KeyIndexTypeId = entityDef.KeyIndexDefId,
                        IdLineViewId = entityDef.IdGroup,
                        IdAttributeId = entityDef.IdItemId,
                        DefaultIndexTypeId = entityDef.DefaultIndexDefId

                    });
                }

            }
            return list;
        }

        public static List<DataContext.DataType> GetDataTypes()
        {
            string jsonData = $$"""
                [
                    {
                        "Id": {{(short)DataTypes.Text}},
                        "Name": "{{DataTypes.Text}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueTexts)}}"
                    },    {
                        "Id": {{(short)DataTypes.numeric}},
                        "Name": "{{DataTypes.numeric}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueNumbers)}}"
                    },    {
                        "Id": {{(short)DataTypes.Lookup}},
                        "Name": "{{DataTypes.Lookup}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueLookups)}}",
                        "LookupTable": "{{nameof(Proton2Context.Lookups)}}"
                      },    {
                        "Id": {{(short)DataTypes.EntityPtr}},
                        "Name": "{{DataTypes.EntityPtr}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueEntities)}}",
                        "LookupTable": "{{nameof(Proton2Context.Entities)}}",                
                        "AltValueTable": "{{nameof(Proton2Context.ValueTexts)}}"
                    },    {
                        "Id": {{(short)DataTypes.LongText}},
                        "Name": "{{DataTypes.LongText}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueLongTexts)}}"
                    },  {
                        "Id": {{(short)DataTypes.Date}},
                        "Name": "{{DataTypes.Date}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueDates)}}"
                    }, {
                        "Id": {{(short)DataTypes.Time}},
                        "Name": "{{DataTypes.Time}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueTimes)}}",
                        "AltValueTable": "{{nameof(Proton2Context.ValueTexts)}}"
                    }, {
                        "Id": {{(short)DataTypes.QualifiedNumbers}},
                        "Name": "{{DataTypes.QualifiedNumbers}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueNumbers)}}",
                        "AltValueTable": "{{nameof(Proton2Context.ValueLookups)}}",
                        "AltLookupTable": "{{nameof(Proton2Context.Lookups)}}"
                    }
                    
                ]
                """;

            return System.Text.Json.JsonSerializer.Deserialize<List<DataContext.DataType>>(jsonData)!;
        }

        public static List<UserStarter> GetUserStarters()
        {
            List<UserStarter> list = [];
            using Proton.Passwd passwd = new();
            using Proton.Menu mnu = new();
            for (short ix = 1; ix <= passwd.NPages; ix++)
            {
                if (passwd.MoveToPage(ix) && passwd.EncryptedPassword.Length > 1 && passwd.FunctionId == 1)
                {
                    short fn = passwd.FunctionParameter;
                    if (mnu.MoveToPage(fn))
                    {
                        list.Add(new()
                        {
                            Id = ix,
                            UserCode = passwd.EncryptedPassword,
                            UserName = passwd.username,
                            MenuId = passwd.FunctionParameter,
                            EntityTypeId = passwd.EntityTypeId,
                            IdLineViewId = passwd.IdLineScreenId,
                            IndexTypeId = passwd.IndexDefId
                        });
                    }
                }

            }

            return list;
        }


        public static List<IndexType> GetIndexTypes()
        {
            List<IndexType> list = [];
            using Proton.IndexDef indexDef = new();
            using Proton.KeyDef keyDef = new();
            {
                for (short i = 1; i <= indexDef.NPages; i++)
                {
                    if (indexDef.MoveToPage(i))
                    {
                        for (short k= 1; k<=indexDef.NPages; k++)
                        {
                            if(keyDef.MoveToPage(k) && keyDef.IndexDefId == i)
                            {
                                list.Add(new()
                                {
                                    Id = i,
                                    Name = keyDef.Name,
                                    EntityTypeId = keyDef.EntityTypeId,
                                    IdLineViewId = indexDef.IdlineScreenId,
                                    Prefix = keyDef.Prefix
                                });
                                break; // only one key per index type
                            }
                        }
                    }
                }
            }
            return list;
        }

        public static List<DataContext.Table> GetTables()
        {
            Dictionary<short, DataContext.Table> list = [];
            using Proton.Screen scrn = new();
            using Proton.TrGroup grp = new();
            using Proton.Item itm = new();
            using Proton.EntityDef ent = new();
            for (short i = 1; i <= itm.NPages; i++)
            {
                if (itm.MoveToPage(i) && itm.IsInstalled && itm.DataType > 0 && itm.EntityTypeId > 0)
                {
                    short tableId = (itm.GroupId == 0) ? (short)-itm.EntityTypeId : itm.GroupId;

                    Table? tbl = list.GetValueOrDefault(tableId);
                    if (tbl == null)
                    {
                        tbl = new()
                        {
                            Id = tableId,
                            Name = itm.Name,
                            EntityTypeId = itm.EntityTypeId,
                            DateAttributeId = itm.DateItemId,
                        };
                        if (tableId > 0)
                        {
                            if (scrn.MoveToPage(tableId))
                            {
                                tbl.Name = scrn.Name;
                            }
                            if (grp.MoveToPage(tableId) &&
                                grp.Name.Length > tbl.Name.Length)
                            {
                                tbl.Name = grp.Name;
                            }
                            string nm = itm.Comment.Replace("date of ", null, StringComparison.CurrentCultureIgnoreCase).Replace("date", null, StringComparison.CurrentCultureIgnoreCase).Trim();
                            if (tbl.Name == null || tbl.Name.Length < nm.Length)
                            {
                                tbl.Name = nm;
                            }
                        }
                        else
                        {
                            if (ent.MoveToPage(tbl.EntityTypeId))
                            {
                                tbl.Name = ent.Name;
                            }
                        }
                        list.Add(tableId, tbl);
                    }
                }
            }

            return list.Values.ToList();
        }


        public static List<DataContext.Attribute> GetAttributes()
        {
            List<DataContext.Attribute> list = new();
            using Proton.Screen scrn = new();
            using Proton.TrGroup grp = new();
            using Proton.Item itm = new();
            using Proton.Valid vald = new();
            for (short i = 1; i <= itm.NPages; i++)
            {
                if (itm.MoveToPage(i) && itm.IsInstalled && itm.DataType > 0 && itm.EntityTypeId > 0)
                {
                    short tableId = (itm.GroupId == 0) ? (short)-itm.EntityTypeId : itm.GroupId;

                    DataContext.Attribute attr = new()
                    {
                        Id = i,
                        EntityTypeId = itm.EntityTypeId,
                        TableId = tableId,
                        DisplayLength = itm.DisplayLength,
                        Name = itm.Name.Length > itm.Comment.Length ? itm.Name : itm.Comment
                    };
                    switch (itm.DataType)
                    {
                        case 1:
                            attr.DataTypeId = (short)DataContext.DataTypes.Text;
                            break;
                        case 2:
                        case 3:
                        case 4:
                            attr.DataTypeId = (short)DataTypes.numeric;
                            break;
                        case 5:
                        case 6:
                            if (vald.MoveToPage(i) && (vald.QualifierCodeType > 0 || vald.ModifierCodeType > 0))
                            {
                                attr.DataTypeId = (short)DataTypes.QualifiedNumbers;
                            }
                            else attr.DataTypeId = (short)DataTypes.numeric;
                            break;
                        case 7:
                            attr.DataTypeId = (short)DataTypes.Lookup;
                            break;
                        case 8:
                            attr.DataTypeId = (short)DataTypes.Date;
                            break;
                        case 9:
                            attr.DataTypeId = (short)DataTypes.Time;
                            break;
                        case 10:
                            attr.DataTypeId = (short)DataTypes.LongText;
                            break;
                        case 11:
                            attr.DataTypeId = (short)DataTypes.EntityPtr;
                            break;
                        case 12:
                            attr.DataTypeId = (byte)DataTypes.Lookup;
                            break;

                        default:
                            throw new Exception("unknown datatype");

                    }
                    if (itm.IsCalculated)
                    {

                        if (vald.MoveToPage(i))
                        {
                            attr.Quark = vald.CalcQuark;
                        }

                    }
                    list.Add(attr);

                }
            }

            return list;
        }

        public static List<DataContext.Menu> GetMenus()
        {
            using Proton.Menu menu = new();
            var list = new List<DataContext.Menu>();
            for (short i = 1; i <= menu.NPages; i++)
            {
                if (menu.MoveToPage(i))
                {
                    list.Add(new()
                    {
                        Id = i,
                        Name = menu.Name
                    });
                
                }
            }
            return list;
        }

        public static List<MenuItem> GetMenuItems()
        {
            using Proton.Menu menu = new();
            var list = new List<MenuItem>();
            for (short i = 1; i <= menu.NPages; i++)
            {
                if (menu.MoveToPage(i))
                {
                    byte c = 0;
                    while (menu.MoveToNextBlock())
                    {
                        list.Add(new()
                        {
                            MenuId = i,
                            Seq = c,
                            Name = menu.ItemName,
                            Function = menu.Function,
                            NextMenuId = menu.NextMenuId,
                            StartMenuId = menu.StartMenuId,
                            Parameter1 = menu.Parameter1,
                            Parameter2 = menu.Parameter2,
                            Parameter3 = menu.Parameter3,
                            Parameter4 = menu.Parameter4
                        });

                        c++;
                    }
                }
            }

            return list;
        }

        public static List<LookupType> GetLookupTypes()
        {
            List<LookupType> codes = [];

            using (Proton.CodeDef cd = new())
            {
                codes.Add(new() { Id = 0, Name = "Proton DICT code" });
                for (short ix = 1; ix <= cd.NPages; ix++)
                {
                    if (cd.MoveToPage(ix))
                    {
                        var ct = ix;
                        if (ct > 0)
                        {
                            codes.Add(new() { Id = ix, Name = cd.Name });
                        }
                    }
                }
            }
            return codes;
        }



        public static List<Lookup> GetLookups()
        {
            List<Lookup> codes = [];

            using (Proton.RCode rcode = new())
            {
                for (int ix = 1; ix <= rcode.NPages; ix++)
                {
                    if (rcode.MoveToPage(ix))
                    {
                      codes.Add(new() { Id = ix, Name = rcode.Name, LookupTypeId = rcode.CodeTypeID, Code = rcode.ReadCode });
                    }
                }
            }
            using (Proton.Dict dict = new())
            {
                for (int ix = 1; ix <= dict.NPages; ix++)
                {
                    if (dict.MoveToPage(ix))
                    {
                        codes.Add(new() { Id = -ix, Name = dict.Name, LookupTypeId = 0 });
                    }
                }
            }

            return codes;
        }


        public static List<View> GetViews()
        {
            List<View> list = [];

            using (Proton.Screen scrn = new())
            using (Proton.ScrTxt scrtxt = new())
            using (Proton.Item itm = new())
            using (Proton.Menu menu = new())
            using (Proton.TrGroup grp = new())

                for (short ix = 1; ix <= scrn.NPages; ix++)
                {
                    if (scrn.MoveToPage(ix) && itm.MoveToPage(scrn.ItemId))
                    {
                        var scrObj = new View()
                        {
                            Id = ix,
                            Name = scrn.Name,
                            TableId = itm.GroupId == 0 ? (short)-itm.EntityTypeId : itm.GroupId,
                            EntityTypeId = itm.EntityTypeId,
                            NRows = scrn.RowCount,
                            NItems = scrn.ItemCount
                        };

                        if (scrn.MoveToPage(itm.GroupId))
                        {
                            if (scrn.Name.Length > scrObj.Name.Length)
                            {
                                scrObj.Name = scrn.Name;
                            }
                            scrn.MoveToPage(ix);
                        }
                        if (grp.MoveToPage(itm.GroupId))
                        {
                            if (grp.Name.Length > scrObj.Name.Length)
                            {
                                scrObj.Name = grp.Name;
                            }
                        }
                        bool breakLoop = false;
                        for (short ixm = 1; ixm <= menu.NPages; ixm++)
                        {
                            if (menu.MoveToPage(ixm))
                            {
                                while (menu.MoveToNextBlock())
                                {
                                    if (menu.Function == "SCRN" && menu.Parameter1 == ix)
                                    {
                                        if (scrObj.Name.Length > menu.ItemName.Length)
                                        {
                                            scrObj.Name = menu.ItemName;
                                            breakLoop = true;
                                            break;
                                        }
                                    }
                                }
                                if (breakLoop) break;
                            }
                        }

                        if (scrtxt.MoveToPage(ix))
                        {
                            string nm = scrtxt.Text.Replace("date of", "", StringComparison.CurrentCultureIgnoreCase).Replace("date at", "", StringComparison.OrdinalIgnoreCase).Replace("date", "", StringComparison.OrdinalIgnoreCase).Trim();
                            if (nm.Length > scrObj.Name.Length)
                            {
                                scrObj.Name = nm;
                            }
  
                        }
                        list.Add(scrObj);
                    }
                }

            return list;
        }

        class ViewAttributeEquality : EqualityComparer<ViewAttribute>
        {
            public override bool Equals(ViewAttribute? b1, ViewAttribute? b2)
            {
                if (b1 == null && b2 == null)
                    return true;
                else if (b1 == null || b2 == null)
                    return false;

                return (b1.ViewId == b2.ViewId &&
                        b1.Seq == b2.Seq) ;
            }

            public override int GetHashCode(ViewAttribute bx)
            {
                int hCode = bx.ViewId ^ bx.Seq;
                return hCode.GetHashCode();
            }
        }

        public static List<ViewAttribute> GetViewAttributes()
        {
            List<ViewAttribute> list = [];

            using (Proton.Screen scrn = new())
            using (Proton.Item itm = new())

            for (short ix = 1; ix <= scrn.NPages; ix++)
            {
                if (scrn.MoveToPage(ix) )
                {
                    short seq = 0;
                    while (scrn.MoveToNextBlock() && ( scrn.X * scrn.Y) > 0)
                    {
                        short itemId = scrn.ItemId;
                        if (itemId>0 && itm.MoveToPage(itemId) && itm.IsInstalled)
                        {
                            list.Add(new()
                            {
                                ViewId = ix,
                                AttributeId = itemId,
                                Seq = seq,
                                X = scrn.X,
                                Y = scrn.Y
                            });
                            seq++;
                        }
                    }
                }
            }
            return list.Distinct(new ViewAttributeEquality() ).ToList();
        }

        public static List<ViewCaption> GetViewCaptions()
        {
            List<ViewCaption> list = new();
            using (Proton.ScrTxt scrtxt = new())
            for (short ix = 1; ix <= scrtxt.NPages; ix++)
            {
                if (scrtxt.MoveToPage(ix))
                {
                    short seq = 0;
         
                    while (scrtxt.MoveToNextBlock() && (scrtxt.Text.Length * scrtxt.X * scrtxt.Y) > 0)
                    {
                        list.Add(new()
                        {
                            ViewId = ix,
                            Seq = seq,
                            Caption = scrtxt.Text,
                            X = scrtxt.X,
                            Y = scrtxt.Y
                        });
                        seq++;
                    }
                }
            }
            return list;
        }

    }
}
