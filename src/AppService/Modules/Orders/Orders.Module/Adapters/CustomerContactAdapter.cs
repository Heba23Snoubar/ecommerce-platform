using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Orders.Module.Ports;

namespace Orders.Module.Adapters;

internal class CustomerContactAdapter : ICustomerContact
{
    private readonly IHttpContextAccessor _http;

    public CustomerContactAdapter(IHttpContextAccessor http) => _http = http;

    public CustomerContact? GetCurrent()
    {
        var user = _http.HttpContext?.User;
        var email = user?.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var name = user!.FindFirstValue(ClaimTypes.Name) ?? email;
        return new CustomerContact(name, email);
    }
}