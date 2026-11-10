using Microsoft.AspNetCore.Mvc;

namespace Shared.HttpCaching;

public sealed class ETagAttribute : TypeFilterAttribute
{
    public ETagAttribute() : base(typeof(ETagResultFilter))
    {
    }
}