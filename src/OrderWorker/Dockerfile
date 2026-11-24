FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY src/OrderWorker/publish .
ENTRYPOINT ["dotnet", "OrderWorker.dll"]
