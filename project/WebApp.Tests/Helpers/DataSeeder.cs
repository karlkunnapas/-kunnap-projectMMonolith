using App.DAL.EF;
using App.DAL.EF.Seeding;

namespace WebApp.Tests.Helpers;

public static class DataSeeder
{
    public static void SeedData(AppDbContext ctx)
    {
        AppDataInit.SeedAppData(ctx);
    }
}