#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["./g2yx.Console/g2yx.ConsoleApp.csproj", "./g2yx.Console/"]
COPY ["./g2yx.Services/g2yx.Services.csproj", "./g2yx.Services/"]
RUN dotnet restore "./g2yx.Console/g2yx.ConsoleApp.csproj"
COPY . .
WORKDIR "./g2yx.Console"
RUN dotnet build "g2yx.ConsoleApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "g2yx.ConsoleApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "g2yx.ConsoleApp.dll"]