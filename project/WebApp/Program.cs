using App.BLL.Services;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using WebApp.Helpers;
using WebApp.Setup;

var builder = WebApplication.CreateBuilder(args);

// Service registration
builder.Services.AddAppDatabase(builder.Configuration, builder.Environment);
builder.Services.AddAppIdentity();
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
builder.Services.AddScoped<IChargingStationService, ChargingStationService>();

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
