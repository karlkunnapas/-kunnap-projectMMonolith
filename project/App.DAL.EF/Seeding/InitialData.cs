namespace App.DAL.EF.Seeding;

public static class InitialData
{
    public static readonly (string roleName, Guid? id)[]
        Roles =
        [
            ("Admin", null),
            ("Customer", null),
            ("MaintenancePersonnel", null),
            ("root", null),
        ];

    public static readonly (string name, string password, Guid? id, string[] roles)[]
        Users =
        [
            ("karl@karl.com", "karl", null, ["Customer"]),
            ("admin@admin.com", "admin", null, ["Admin"]),
        ];
}