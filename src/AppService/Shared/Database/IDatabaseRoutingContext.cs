using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Database
{
    public interface IDatabaseRoutingContext
    {
        bool ShouldUsePrimaryDatabase();
        void MarkWriteOperation();
        string ResolveConnectionString(string primaryConnectionString, string? replicaConnectionString = null);
    }
}
 
