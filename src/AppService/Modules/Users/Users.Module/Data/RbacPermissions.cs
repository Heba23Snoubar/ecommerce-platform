namespace Users.Module.Data;

internal static class RbacPermissions
{
    internal static readonly string[] All =
    [
        // Users
        "Users.Read",
        "Users.Create",
        "Users.Update",
        "Users.Delete",
        "Users.ManageRoles",

        // Products
        "Products.Read",
        "Products.Create",
        "Products.Update",
        "Products.Delete",

        // Orders
        "Orders.Read",
        "Orders.UpdateStatus",
        "Orders.Cancel",

        // Roles
        "Roles.Read",
        "Roles.Create",
        "Roles.Update",
        "Roles.Delete",
        "Roles.ManagePermissions"
    ];
}