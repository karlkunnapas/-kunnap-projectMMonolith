using Microsoft.Extensions.Configuration;

namespace WebApp.Helpers;

public class AppNameService
{
    public string AppName { get; set; } = "WebApp";
    private readonly IConfiguration _configuration;
    
    public AppNameService(IConfiguration configuration)
    {
        _configuration = configuration;
        AppName = _configuration.GetValue<string>("AppName") ??  "WebApp";
    }
}