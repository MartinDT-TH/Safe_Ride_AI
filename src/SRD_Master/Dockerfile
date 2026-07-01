FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["SafeRide.API/SafeRide.API.csproj", "SafeRide.API/"]
COPY ["SafeRide.Application/SafeRide.Application.csproj", "SafeRide.Application/"]
COPY ["SafeRide.Contracts/SafeRide.Contracts.csproj", "SafeRide.Contracts/"]
COPY ["SafeRide.Domain/SafeRide.Domain.csproj", "SafeRide.Domain/"]
COPY ["SafeRide.Shared/SafeRide.Shared.csproj", "SafeRide.Shared/"]
COPY ["SafeRide.Infrastructure/SafeRide.Infrastructure.csproj", "SafeRide.Infrastructure/"]
COPY ["SafeRide.Realtime/SafeRide.Realtime.csproj", "SafeRide.Realtime/"]
COPY ["SafeRide.Infrastructure/SafeRide.Infrastructure.csproj", "SafeRide.Infrastructure/"]
COPY ["SafeRide.Realtime/SafeRide.Realtime.csproj", "SafeRide.Realtime/"]

RUN dotnet restore "SafeRide.API/SafeRide.API.csproj"

COPY . .
WORKDIR "/src/SafeRide.API"
RUN dotnet publish "SafeRide.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SafeRide.API.dll"]