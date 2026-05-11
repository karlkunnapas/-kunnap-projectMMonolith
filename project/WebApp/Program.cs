using App.BLL.Services;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using Mediator;
using Modules.Charging;
using Modules.Companies;
using Modules.Users;
using WebApp.Filters;
using WebApp.Helpers;
using WebApp.Setup;

[assembly: MediatorOptions(ServiceLifetime = ServiceLifetime.Scoped, Namespace = "WebApp.Mediator")]

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Service registration
builder.Services.AddAppDatabase(builder.Configuration, builder.Environment);
builder.Services.AddAppIdentity(builder.Configuration);
builder.Services.AddUsersModule(connectionString);
builder.Services.AddCompaniesModule(connectionString);
builder.Services.AddChargingModule(connectionString);
builder.Services.AddMediator();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AppNameService>();
builder.Services.AddAppControllers();
builder.Services.AddForwardedHeaders();
builder.Services.AddAppCors();
builder.Services.AddAppApiVersioning();
builder.Services.AddAppSwagger();
builder.Services.AddAppLocalization(builder.Configuration);
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IHomePageService, HomePageService>();
builder.Services.AddScoped<IChargingStationService, ChargingStationService>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IVehicleCompatibilityService, VehicleCompatibilityService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IChargingSessionService, ChargingSessionService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<IOperatorDashboardService, OperatorDashboardService>();
builder.Services.AddScoped<IAdminPanelService, AdminPanelService>();
builder.Services.AddScoped<IChargingStationCompanyService, ChargingStationCompanyService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<EnsureActiveCompanyAccessFilter>();

// Build and configure pipeline 
var app = builder.Build();

app.SetupAppData();
app.UseAppMiddleware();
app.UseAppSwagger();
app.MapAppEndpoints();

app.Run();

// this is needed for unit testing
// ReSharper disable once ClassNeverInstantiated.Global
public partial class Program
{
}
