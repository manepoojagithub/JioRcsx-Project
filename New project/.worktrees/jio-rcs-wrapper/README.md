# Advait Services

Enterprise ASP.NET Core MVC wrapper panel for the documented JioCX RCS UAT APIs. The solution uses Clean Architecture, EF Core Code First, SQL Server, Repository + Unit of Work, Cookie/JWT hybrid auth, RBAC, SignalR, and a durable database-backed campaign queue.

## Projects

- `src/JioCxRcsWrapper.Domain`: entities and enums.
- `src/JioCxRcsWrapper.Application`: services, validators, contracts, RBAC, reports, queue policy.
- `src/JioCxRcsWrapper.Infrastructure`: EF Core, repositories, encryption, JioCX client, exports, queue worker.
- `src/JioCxRcsWrapper.Web`: MVC controllers, Razor views, SignalR hubs, AJAX scripts.
- `tests`: unit and integration coverage.

## Prerequisites

- .NET 9 SDK
- SQL Server or LocalDB
- EF Core tool from the repo tool manifest

```powershell
dotnet tool restore
dotnet restore
```

## Database

Update `ConnectionStrings:DefaultConnection`, then apply migrations:

```powershell
dotnet tool run dotnet-ef database update --project src/JioCxRcsWrapper.Infrastructure --startup-project src/JioCxRcsWrapper.Web
```

Seeded admin:

- Email: `admin@local.test`
- Password: `ChangeMe@12345`

Change this immediately in production.

## Supported JioCX APIs

Only the APIs present in the provided UAT document are implemented:

- `POST https://rcsapi-uat.jiocx.com/api/v1/uploadFile`
- `POST https://rcsapi-uat.jiocx.com/api/v1/sendMessage`
- `POST https://rcsapi-uat.jiocx.com/api/v1/checkCapability`

The app does not implement undocumented onboarding or tester APIs. Dialer, calendar, and location CTAs are visible as draft choices but blocked from send payload generation because the document does not define their payload schema.

## Run

```powershell
dotnet run --project src/JioCxRcsWrapper.Web
```

Open the URL printed by Kestrel and log in as the seeded admin.

## Production Settings

Set these through environment variables, user secrets, or your hosting provider:

- `ConnectionStrings__DefaultConnection`
- `Jwt__SigningKey`
- `JioCx__BaseUrl`
- `Queue__Enabled`
- `Queue__BatchSize`
- `Queue__MaxAttempts`
- `Queue__PollSeconds`

API keys are encrypted with ASP.NET Core Data Protection. Persist Data Protection keys outside the app directory in production.

## Verification

```powershell
dotnet format --verify-no-changes
dotnet build -c Release
dotnet test -c Release
```
