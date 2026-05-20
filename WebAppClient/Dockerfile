FROM mcr.microsoft.com/dotnet/sdk:latest AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY WebAppClient.sln .
COPY WebAppClient.csproj .
RUN dotnet restore "WebAppClient.csproj"

# copy everything else and build app
COPY . .
RUN dotnet publish "WebAppClient.csproj" -c Release -o /app/out --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:latest AS runtime
WORKDIR /app
EXPOSE 8084
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "WebAppClient.dll"]
