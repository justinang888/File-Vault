# Build stage: compile and publish the app using the .NET 8 SDK.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["FileSharingandStorageSystem.csproj", "./"]
RUN dotnet restore "FileSharingandStorageSystem.csproj"

COPY . .
RUN dotnet publish "FileSharingandStorageSystem.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage: only the ASP.NET runtime, no SDK, for a smaller image.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Hosts like Render/Fly route to the port in $PORT; bind Kestrel to it (default 8080).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FileSharingandStorageSystem.dll"]
