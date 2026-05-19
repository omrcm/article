# Multi-stage build — ARM64 + AMD64 destekli
# Mac M-serisi'de native ARM64 olarak build edilir, GCP'de AMD64 olur
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app ./

# Performans için: server GC + tiered compilation default açık
ENV DOTNET_gcServer=1
ENV DOTNET_TieredCompilation=1
ENV ASPNETCORE_URLS=http://+:5000

EXPOSE 5000
ENTRYPOINT ["dotnet", "ThreadingDemo.dll"]
