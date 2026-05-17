FROM mcr.microsoft.com/dotnet/sdk:latest AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY project.sln .
COPY Directory.Build.props .
COPY App.BLL/*.csproj ./App.BLL/
COPY App.DAL.EF/*.csproj ./App.DAL.EF/
COPY App.DTO/*.csproj ./App.DTO/
COPY App.Domain/*.csproj ./App.Domain/
COPY App.Helpers/*.csproj ./App.Helpers/
COPY App.Resources/*.csproj ./App.Resources/
COPY Base.Resources/*.csproj ./Base.Resources/
COPY Modules.Charging/*.csproj ./Modules.Charging/
COPY Modules.Companies/*.csproj ./Modules.Companies/
COPY Modules.Users/*.csproj ./Modules.Users/
COPY Shared.Contracts/*.csproj ./Shared.Contracts/
COPY WebApp/*.csproj ./WebApp/
RUN dotnet restore "WebApp/WebApp.csproj"

# copy everything else and build app
COPY . .
RUN dotnet publish "WebApp/WebApp.csproj" -c Release -o /app/out --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:latest AS runtime
WORKDIR /app
EXPOSE 8083
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "WebApp.dll"]
