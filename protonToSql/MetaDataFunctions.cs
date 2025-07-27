using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Tokens;
using NetTopologySuite.Index.HPRtree;
using ProtonConsole2.DataContext;
using ProtonConsole2.DataContext.ProtonUi;
using ProtonConsole2.Proton;
using ProtonConsole2.ProtonToSql;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;
using System.Transactions;

namespace ProtonConsole2.ProtonBinaryReaders
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
                        EntityTypeId = ix,
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
                        "DataTypeId": {{(short)DataTypes.Text}},
                        "Name": "{{DataTypes.Text}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueTexts)}}"
                    },    {
                        "DataTypeId": {{(short)DataTypes.numeric}},
                        "Name": "{{DataTypes.numeric}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueNumbers)}}"
                    },    {
                        "DataTypeId": {{(short)DataTypes.Lookup}},
                        "Name": "{{DataTypes.Lookup}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueLookups)}}",
                        "LookupTable": "{{nameof(Proton2Context.Lookups)}}"
                      },    {
                        "DataTypeId": {{(short)DataTypes.EntityPtr}},
                        "Name": "{{DataTypes.EntityPtr}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueEntities)}}",
                        "LookupTable": "{{nameof(Proton2Context.Entities)}}",                
                        "AltValueTable": "{{nameof(Proton2Context.ValueTexts)}}"
                    },    {
                        "DataTypeId": {{(short)DataTypes.LongText}},
                        "Name": "{{DataTypes.LongText}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueLongTexts)}}"
                    },  {
                        "DataTypeId": {{(short)DataTypes.Date}},
                        "Name": "{{DataTypes.Date}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueDates)}}"
                    }, {
                        "DataTypeId": {{(short)DataTypes.Time}},
                        "Name": "{{DataTypes.Time}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueTimes)}}",
                        "AltValueTable": "{{nameof(Proton2Context.ValueTexts)}}"
                    }, {
                        "DataTypeId": {{(short)DataTypes.QualifiedNumbers}},
                        "Name": "{{DataTypes.QualifiedNumbers}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueNumbers)}}",
                        "AltValueTable": "{{nameof(Proton2Context.ValueLookups)}}",
                        "AltLookupTable": "{{nameof(Proton2Context.Lookups)}}"
                    }
                    
                ]
                """;

            return System.Text.Json.JsonSerializer.Deserialize<List<DataContext.DataType>>(jsonData);
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
                            UserStarterId = ix,
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

        public static List<DataContext.Table> GetTables()
        {
            Dictionary<short, DataContext.Table> list = [];
            using Proton.Screen scrn = new();
            using Proton.TrGroup grp = new();
            using Proton.Item itm = new();
            using Proton.EntityDef ent = new();
            using Proton.Valid vald = new();
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
                            TableId = tableId,
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
                    DataContext.Attribute attr = new()
                    {
                        AttributeId = (short)itm.ItemId,
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
                    tbl.Attributes.Add(attr);

                }
            }

            return list.Values.ToList();
        }


        public static List<DataContext.Menu> GetMenus()
        {
            using Proton.Menu menu = new();
            var list = new List<DataContext.Menu>();
            for (short i = 1; i <= menu.NPages; i++)
            {
                if (menu.MoveToPage(i))
                {
                    DataContext.Menu mnu = new() { MenuId = i, Name = menu.Name };
                    byte c = 0;
                    while (menu.MoveToNextBlock())
                    {
                        mnu.Items.Add(new()
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
                    list.Add(mnu);
                }
            }

            return list;

        }




        //await context.BulkInsertAsync(GetEntityInstances(context));

        //var tables = GetTables(context);


        //uint eid = 11134;
        //List<TableRow> tableRows = GetTableRows(eid);

        //await context.BulkInsertAsync(tableRows, b => b.IncludeGraph = true);
        ////await context.BulkInsertAsync(GetValueTexts(tableRows));
        //var gt = GetValueTexts(tableRows);

        //uint eid = 11134;
        //using (var transaction = context.Database.BeginTransaction())
        //{
        //    var tableRows = GetTableRows(eid);
        //    List<ValueText> valueTexts = new();
        //    await context.BulkInsertAsync(tableRows, new BulkConfig { SetOutputIdentity = true });
        //    foreach (var row in tableRows)
        //    {
        //        foreach (var valueText in row.ValueTexts)
        //        {
        //            valueText.TableRowId = row.TableRowId; // sets FK to match linked PK that was generated in DB
        //        }
        //        valueTexts.AddRange(row.ValueTexts);
        //    }
        //    await context.BulkInsertAsync(valueTexts);
        //    transaction.Commit();
        //}


        public static List<LookupType> GetLookupTypes()
        {
            List<LookupType> codes = [];

            using (Proton.CodeDef cd = new())
            {
                codes.Add(new() { LookupTypeId = 0, Name = "Proton DICT code" });
                for (short ix = 1; ix <= cd.NPages; ix++)
                {
                    if (cd.MoveToPage(ix))
                    {
                        var ct = cd.CodeDefId;
                        if (ct > 0)
                        {
                            codes.Add(new() { LookupTypeId = ix, Name = cd.Name });
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
                        var ct = rcode.CodeTypeID;

                        codes.Add(new() { LookupId = ix, Name = rcode.Name, LookupTypeId = rcode.CodeTypeID, Code = rcode.ReadCode });

                    }
                }
            }
            using (Proton.Dict dict = new())
            {
                for (int ix = 1; ix <= dict.NPages; ix++)
                {
                    if (dict.MoveToPage(ix))
                    {
                        codes.Add(new() { LookupId = -ix, Name = dict.Name, LookupTypeId = 0 });
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
                        ViewId = ix,
                        Name = scrn.Name,
                        TableId = itm.GroupId == 0 ? (short)-itm.EntityTypeId: itm.GroupId,
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
          
       
                    short seq = 0;
                    while (scrn.MoveToNextBlock() && (scrn.ItemId * scrn.X * scrn.Y) > 0)
                    {
                        var itemId = scrn.ItemId;
                        if (itm.MoveToPage(itemId) && itm.IsInstalled)
                        {
                            scrObj.ViewAttributes.Add(new()
                            {
                                ViewId = ix,
                                AttributeId = scrn.ItemId,
                                Seq = seq,
                                X = scrn.X,
                                Y = scrn.Y
                            });
                            seq++;
                        }
                    }
                    if (scrtxt.MoveToPage(ix))
                    {
                        seq = 0;
                        string nm = scrtxt.Text.Replace("date of", "", StringComparison.CurrentCultureIgnoreCase).Replace("date at", "", StringComparison.OrdinalIgnoreCase).Replace("date", "", StringComparison.OrdinalIgnoreCase).Trim();
                        if (nm.Length > scrObj.Name.Length)
                        {
                            scrObj.Name = nm;
                        }
                        while (scrtxt.MoveToNextBlock() && (scrtxt.Text.Length * scrtxt.X * scrtxt.Y) > 0)
                        {
                    
                            scrObj.ViewCaptions.Add(new()
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
                    list.Add(scrObj);

                        
                }
            }

            return list;
            

        }

    }
}
