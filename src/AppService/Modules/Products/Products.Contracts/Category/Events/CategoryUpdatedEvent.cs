using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Products.Contract.Category.Events
{

    public record CategoryUpdatedEvent(
        Guid CategoryId
    );
}
