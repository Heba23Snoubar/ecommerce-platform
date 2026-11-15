FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/OrderWorker/OrderWorker.csproj", "src/OrderWorker/"]
RUN dotnet restore "src/OrderWorker/OrderWorker.csproj"

COPY ["src/OrderWorker/", "src/OrderWorker/"]
WORKDIR "/src/src/OrderWorker"
RUN dotnet publish "OrderWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderWorker.dll"]