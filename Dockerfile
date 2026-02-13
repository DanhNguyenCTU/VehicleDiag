# Stage 1 - Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY . ./
RUN dotnet restore VehicleDiag.Api/VehicleDiag.Api.csproj
RUN dotnet publish VehicleDiag.Api/VehicleDiag.Api.csproj -c Release -o out

# Stage 2 - Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "VehicleDiag.Api.dll"]
