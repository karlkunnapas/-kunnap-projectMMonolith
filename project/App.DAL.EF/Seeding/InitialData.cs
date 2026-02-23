namespace App.DAL.EF.Seeding;

public static class InitialData
{
    public static readonly (string roleName, Guid? id)[]
        Roles =
        [
            ("admin", null),
            ("user", null),
            ("root", null),
        ];

    public static readonly (string name, string password, Guid? id, string[] roles)[]
        Users =
        [
            ("akaver@akaver.com", "Kala.Maja.101", null, ["admin", "root", "user"]),
            ("andres.kaver@taltech.ee", "Kala.Maja.101", null, ["admin", "root", "user"]),
        ];
}