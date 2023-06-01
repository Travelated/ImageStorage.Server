FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["ImageStorage/ImageStorage.Server/ImageStorage.Server.csproj", "ImageStorage/ImageStorage.Server/"]
RUN dotnet restore "ImageStorage/ImageStorage.Server/ImageStorage.Server.csproj"
COPY . .
WORKDIR "/src/ImageStorage/ImageStorage.Server"
RUN dotnet build "ImageStorage.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ImageStorage.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ImageStorage.Server.dll"]
