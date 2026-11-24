FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/OrderWorker/OrderWorker.csproj src/OrderWorker/
RUN dotnet restore src/OrderWorker/OrderWorker.csproj
COPY src/OrderWorker/ src/OrderWorker/
RUN dotnet publish src/OrderWorker/OrderWorker.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "OrderWorker.dll"]