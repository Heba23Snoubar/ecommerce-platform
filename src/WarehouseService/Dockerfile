FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY src/WarehouseService/out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Warehouse.Api.dll"]
