FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app


COPY *.sln .
COPY gateway/*.csproj ./gateway/
COPY HotelService.UnitTests/*.csproj ./HotelService.UnitTests/
RUN dotnet restore

COPY . .
WORKDIR /app/gateway
RUN dotnet publish -c Release -o /app/publish


FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "gateway.dll"]