using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.ProtonToSql
{
    internal class EntityTypeReader : IDisposable
    {
        private static Dictionary<short, EntityType> _entityTypes;

        static EntityTypeReader()
        {
            using Proton.EntityDef entityDef = new();
            _entityTypes = [];
            for(short i = 1; i <= entityDef.NPages; i++)
            {
                if (entityDef.MoveToPage(i))
                {
                    _entityTypes.Add(i, new() 
                    {
                         Id = i,
                         Name= entityDef.Name,
                         DefaultIndexTypeId= entityDef.DefaultIndexDefId,
                         KeyIndexTypeId = entityDef.KeyIndexDefId,
                         IdLineViewId= entityDef.IdGroup,
                    });
                }
            }
        }

        public static List<EntityType> GetEntityTypes ()
        {
            return _entityTypes.Values.ToList();
        }

        public static EntityType? GetEntityType(short entityTypeId)
        {
            return _entityTypes.GetValueOrDefault(entityTypeId);
        }

        public void Dispose()
        {
            _entityTypes.Clear();
        }
    }

    internal class IndexTypeReader : IDisposable
    {
        private static Dictionary<short, IndexType> _indexTypes;

        static IndexTypeReader()
        {
            using Proton.IndexDef indexDef = new();
            using Proton.KeyDef keyDef = new();
            {
                _indexTypes = [];
                for (short i = 1; i <= keyDef.NPages; i++)
                {
                    if (keyDef.MoveToPage(i) && keyDef.Name.Length > 0)
                    {
                        var ii = keyDef.IndexDefId;
                        if (indexDef.MoveToPage(ii))
                        {
                            if (!_indexTypes.ContainsKey(ii))
                            {
                                _indexTypes.Add(ii, new()
                                {
                                    Id = ii,
                                    Name = keyDef.Name,
                                    EntityTypeId =  keyDef.EntityTypeId,
                                    MiddleIndexId = indexDef.IndexIdMiddle,
                                    StartIndexId = indexDef.IndexIdStart,
                                    IdLineViewId = indexDef.IdlineScreenId,
                                    KeyLength = indexDef.KeyLength,
                                    Prefix = keyDef.Prefix,
                                });
                            }
                        }
                    }
                }
            }
        }

        public static DataContext.IndexType? GetIndexType(short indexTypeId)
        {
            return _indexTypes.GetValueOrDefault(indexTypeId);
        }

        public static List<DataContext.IndexType> GetIndexTypes()
        {
            return _indexTypes.Values.ToList();
        }

        public void Dispose()
        {
            _indexTypes.Clear();
        }
    }

    internal class IndexReader : IDisposable
    {
        Proton.Index _index = new();
        private IndexType _indexType ;

        private void SetStartIndexId(int startIndexId)
        {
            if (_index.MoveToPage(startIndexId))
            {
                if (_index.IndexDefId != _indexType.Id) throw new InvalidDataException("Missmatch index types");
            }
            else throw new InvalidDataException("invalid startIndexId: " + startIndexId.ToString());
        }


        private void SetIndexType(short indexTypeId)
        {
            IndexType? it = IndexTypeReader.GetIndexType(indexTypeId);
            if (it == null) throw new InvalidDataException("invalid indexType: " + indexTypeId.ToString());
            _indexType = it;
            _index.SetBlockLength(_indexType.KeyLength);
        }

        private void SetDefaultIndex(short entityTypeId)
        {
            EntityType? et = EntityTypeReader.GetEntityType(entityTypeId);
            if (et == null) throw new InvalidDataException("invalid entitypeId: " + entityTypeId.ToString());
            SetIndexType(et.DefaultIndexTypeId);
        }

        private void SetKeyIndex(short entityTypeId)
        {
            EntityType? et = EntityTypeReader.GetEntityType(entityTypeId);
            if (et == null) throw new InvalidDataException("invalid entitypeId: " + entityTypeId.ToString());
            SetIndexType(et.KeyIndexTypeId);
        }

        private EntityType GetIndexType(short entityTypeId)
        {
            EntityType? et = EntityTypeReader.GetEntityType(entityTypeId);
            if (et == null) throw new InvalidDataException("invalid entitypeId: " + entityTypeId.ToString());
            return et;
        }

        public int GetEntityId(short entityTypeId, string key)
        {
            SetKeyIndex(entityTypeId);
            SetStartIndexId(_indexType.StartIndexId);
            while (string.Compare(_index.KeyText, key, true) < 0)
            {
                if (!_index.MoveToNextPage()) return 0;
            }

            _index.MoveToPreviousPage();

            while (string.Compare(_index.KeyText, key) < 0)
            {
                if (!_index.MoveToNextBlock()) return 0;
            }
            if (_index.KeyText.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return _index.EntityId;
            }
            return 0;

        }

        public List<DataContext.Index> GetDefaultIndexes(short entityTypeId, int nItems, int indexId, int ptr)
        {
            var et = GetIndexType(entityTypeId);
            return GetIndexes(et.DefaultIndexTypeId, nItems, indexId, ptr);
        }

        public List<DataContext.Index> GetIndexes(short indexTypeId, int nItems, int indexId, int ptr)
        {
            SetIndexType(indexTypeId);
            if(indexId> 0)
            {
                SetStartIndexId(indexId);
                _index.Pointer= ptr;
                _index.MoveToNextBlock();
                if (!_index.PageIsValid || _index.IndexDefId != indexTypeId) throw new InvalidDataException("invalid indexId:" + indexId);
            } else
            {
                SetStartIndexId(_indexType.StartIndexId);
            }
            int c = 0;
            var keyItems = new List<DataContext.Index>();
            while (c < nItems && _index.MoveToNextBlock())
            {
                if (_index.IndexDefId == _indexType.Id)
                {
                    keyItems.Add(new DataContext.Index()
                    {
                        EntityId = _index.EntityId,
                        Term = _index.KeyText,
                        IndexTypeId = _indexType.Id
                    });

                    c++;
                }
                else break;
            }
            if (keyItems.Count < nItems)
            {
                ptr = 0;
                indexId = 0;
            } else
            {
                ptr = _index.Pointer;
                indexId= _index.IndexId;
            }
            return keyItems;
        }

        public void Dispose()
        {
            _index.Dispose();
        }
    }
}


