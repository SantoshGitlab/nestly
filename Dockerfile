FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app
COPY . ./
RUN dotnet restore
COPY --from=build /root/.dotnet /root/.dotnet
RUN chown -R root:root /root/.dotnet && chmod +x /root/.dotnet/dotnet
RUN dotnet build --configuration Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:3.1
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "myapp.dll"]
