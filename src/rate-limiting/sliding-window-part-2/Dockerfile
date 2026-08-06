FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Önce sadece csproj'u kopyalayıp restore ediyoruz, böylece bağımlılıklar değişmediği sürece cache kullanılır
COPY SlidingWindow.Api.csproj .
RUN dotnet restore

# Geri kalan kaynağı kopyala ve publish et
COPY . .
RUN dotnet publish -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SlidingWindow.Api.dll"]
