FROM mcr.microsoft.com/dotnet/core/sdk:5.0.106 AS builder
WORKDIR /app
COPY . ./
RUN dotnet restore
RUN dotnet build --configuration Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:5.0
WORKDIR /app
COPY --from=builder /app/out .
ENTRYPOINT ["dotnet", "your_project_name.dll"]
