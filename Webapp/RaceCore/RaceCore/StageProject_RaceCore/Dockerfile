FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["StageProject_RaceCore.csproj", "./"]
RUN dotnet restore "StageProject_RaceCore.csproj"

COPY . .
RUN dotnet publish "StageProject_RaceCore.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "StageProject_RaceCore.dll"]