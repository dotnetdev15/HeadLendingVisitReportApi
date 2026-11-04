# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY HeadLendingVisitReportApi.csproj .
RUN dotnet restore HeadLendingVisitReportApi.csproj

COPY . .
RUN dotnet publish HeadLendingVisitReportApi.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Non-root user for better container security
RUN adduser --disabled-password --gecos "" appuser \
    && mkdir -p /app/App_Data/Uploads \
    && chown -R appuser:appuser /app
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

VOLUME ["/app/App_Data/Uploads"]

ENTRYPOINT ["dotnet", "HeadLendingVisitReportApi.dll"]
