# Tech notes

Based on .NET. Everything is running in docker (db and webapp).


### Install or update tooling for .net

~~~bash
dotnet tool update -g dotnet-ef
dotnet tool update -g dotnet-aspnet-codegenerator
dotnet tool update -g Microsoft.Web.LibraryManager.Cli
~~~

## JS Libs

Add htmx and alpine to js libs.
~~~bash
libman install htmx.org --files dist/htmx.min.js 
libman install alpinejs --files dist/cdn.min.js 
~~~

### Generate module database migrations

Run from solution folder.

~~~bash
dotnet ef migrations add InitialUsers --project Modules.Users --startup-project WebApp --context UsersDbContext --output-dir Infrastructure/Migrations
dotnet ef migrations add InitialCompanies --project Modules.Companies --startup-project WebApp --context CompaniesDbContext --output-dir Infrastructure/Migrations
dotnet ef migrations add InitialCharging --project Modules.Charging --startup-project WebApp --context ChargingDbContext --output-dir Infrastructure/Migrations

dotnet ef database update --project Modules.Users --startup-project WebApp --context UsersDbContext
dotnet ef database update --project Modules.Companies --startup-project WebApp --context CompaniesDbContext
dotnet ef database update --project Modules.Charging --startup-project WebApp --context ChargingDbContext
~~~

## Generate identity UI

Install Microsoft.VisualStudio.Web.CodeGeneration.Design to WebApp.  
Run from inside the WebApp directory. Identity storage is owned by `Modules.Users`.

~~~bash
dotnet aspnet-codegenerator identity -dc Modules.Users.Infrastructure.UsersDbContext -f  
~~~

## Generate controllers

Run from inside the WebApp directory.    
Don't forget to add ***Microsoft.VisualStudio.Web.CodeGeneration.Design*** package to the WebApp project as a NuGet package reference.

MVC Web Controllers (disable global warnings as errors - otherwise only one controller will be generated, then compile starts to fail)

~~~bash
dotnet aspnet-codegenerator controller -name ExampleController -actions -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
~~~

API Controllers

~~~bash
dotnet aspnet-codegenerator controller -name ExampleController -actions -outDir ApiControllers -api --useAsyncActions -f
~~~
