#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["g2yx.csproj", ""]
RUN dotnet restore "./g2yx.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "g2yx.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "g2yx.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "g2yx.dll"]