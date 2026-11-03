# =========================================
# Stage 1: Build the application
# =========================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AppService/AppService.Host -c Release -o /out

# =========================================
# Stage 2: Run the application
# =========================================

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AppService.Host.dll"]