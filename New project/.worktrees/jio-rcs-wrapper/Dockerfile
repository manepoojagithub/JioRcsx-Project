FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY JioCxRcsWrapper.sln ./
COPY .config ./.config
COPY src ./src
RUN dotnet restore src/JioCxRcsWrapper.Web/JioCxRcsWrapper.Web.csproj
RUN dotnet publish src/JioCxRcsWrapper.Web/JioCxRcsWrapper.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "JioCxRcsWrapper.Web.dll"]
