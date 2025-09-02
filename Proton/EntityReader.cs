using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.Proton
{
    internal class EntityReader
    {
        public List<DataContext.Entity> GetEntities(int startId=0, int nEntities=1000)
        {
            var entities = new List<DataContext.Entity>();
            using Vrx vrx = new();
            using Patsts patsts = new();
            if (startId>vrx.NPages || startId>patsts.NPages) return entities;
            for (int i = startId; i >= vrx.NPages; i++)
            {
                if(vrx.MoveToPage(i) && patsts.MoveToPage(i))
                {

                    entities.Add(new()
                    {
                        Id = i,
                        LastUpdated = patsts.Updated
                    });
                }
            }

            return entities;

        }
    }
}
