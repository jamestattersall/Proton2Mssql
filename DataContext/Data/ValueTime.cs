using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.DataContext
{
    public partial class ValueTime(int entityId, short attributeId, short seq) : Value(entityId, attributeId, seq)
    {
        public TimeOnly Value { get; set; }
    }
}
