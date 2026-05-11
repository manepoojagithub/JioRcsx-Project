# Deployment Guide

## IIS

1. Install the .NET 9 Hosting Bundle on the server.
2. Create a SQL Server database and login.
3. Publish:

```powershell
dotnet publish src/JioCxRcsWrapper.Web/JioCxRcsWrapper.Web.csproj -c Release -o .\publish
```

4. Configure the IIS site to point to `publish`.
5. Set environment variables:

```powershell
ConnectionStrings__DefaultConnection=<sql-server-connection-string>
Jwt__SigningKey=<32+ character production secret>
JioCx__BaseUrl=https://rcsapi.jiocx.com
Queue__Enabled=true
```

6. Persist Data Protection keys, for example to a protected folder or key vault-backed provider.
7. Apply migrations:

```powershell
dotnet tool run dotnet-ef database update --project src/JioCxRcsWrapper.Infrastructure --startup-project src/JioCxRcsWrapper.Web
```

## Kestrel Reverse Proxy

Publish the app, run it behind IIS, Nginx, or another reverse proxy, and forward HTTPS headers. Keep `ASPNETCORE_ENVIRONMENT=Production`.

## Operational Notes

- The queue worker is enabled by `Queue:Enabled`.
- 429 and 500 responses are retried with 1, 5, 15, and 60 minute backoff.
- 401 responses from browser AJAX redirect to login.
- 403 responses show "Not allowed".
- When credits reach zero, send/upload/queue actions are disabled and reports remain downloadable.
