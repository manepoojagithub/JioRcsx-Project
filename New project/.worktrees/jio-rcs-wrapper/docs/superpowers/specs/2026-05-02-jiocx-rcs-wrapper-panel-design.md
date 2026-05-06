# JioCX RCS Wrapper Panel Design

## Goal

Build a production-ready enterprise "JioCX RCS Wrapper Panel" as a single ASP.NET Core 9 MVC solution using Clean Architecture, Entity Framework Core Code First, SQL Server, Repository Pattern, Unit of Work, Razor views, jQuery/AJAX, SignalR, hybrid cookie/JWT authentication, and a database-backed campaign send queue.

## Source API Contract

The implementation must use only the APIs documented in `C:\Users\usern\Downloads\JioCX RCS UAT API Document (1).pdf`.

Documented JioCX UAT endpoints:

- `POST https://rcsapi-uat.jiocx.com/api/v1/uploadFile`
- `POST https://rcsapi-uat.jiocx.com/api/v1/sendMessage`
- `POST https://rcsapi-uat.jiocx.com/api/v1/checkCapability`

The product must not implement or call undocumented JioCX APIs. Brand onboarding is handled outside the API through JioCX forms/email. Tester management is not implemented because the document states that the tester API is "Coming soon". The panel stores supplied Agent ID and API key after onboarding, but does not call a JioCX onboarding API.

## Architecture

The solution will contain four projects:

- `JioCxRcsWrapper.Domain`: entities, enums, domain rules, and shared abstractions. This project has no EF Core or MVC dependency.
- `JioCxRcsWrapper.Application`: service interfaces and use cases for authentication, RBAC, client onboarding, campaign creation, message validation, queueing, reports, dashboard metrics, webhook processing, and audit logging.
- `JioCxRcsWrapper.Infrastructure`: EF Core SQL Server `DbContext`, repositories, Unit of Work, migrations, encryption, JioCX HTTP client, CSV/PDF export, queue processing, and retry logic.
- `JioCxRcsWrapper.Web`: MVC controllers, Razor views, AJAX endpoints, SignalR hubs, auth middleware/filters, permission-aware UI helpers, static uploads, and dynamic branding.

Campaign sends will not call JioCX directly from MVC button clicks. The UI creates or schedules campaigns, validates contacts and message payloads, stores durable queue rows in SQL Server, and a hosted background service processes queue items. This supports retries, avoids request timeouts, preserves logs, and allows SignalR to broadcast real-time progress.

## Data Model

The database will be created with EF Core Code First migrations.

Required tables:

- `Users`: `Id`, `Name`, `Email`, `PasswordHash`, `RoleId`, `ClientId`, `IsActive`, `CreatedAt`
- `Roles`: `Id`, `Name`
- `Permissions`: `Id`, `Name`
- `RolePermissions`: `Id`, `RoleId`, `PermissionId`
- `Clients`: `Id`, `BrandName`, `AgentName`, `AgentId`, `ApiKey`, `LogoPath`, `SiteName`, `Credits`, `CreatedBy`, `CreatedAt`
- `Campaigns`: `Id`, `Name`, `ClientId`, `Type`, `Status`, `CreatedBy`, `CreatedAt`, `ScheduledAt`
- `CampaignMessages`: `Id`, `CampaignId`, `PayloadJson`, `MessageType`
- `Contacts`: `Id`, `CampaignId`, `MobileNumber`, `Status`
- `MessageLogs`: `Id`, `CampaignId`, `ContactId`, `Status`, `ErrorCode`, `Response`, `Timestamp`
- `Reports`: `Id`, `CampaignId`, `TotalSent`, `Delivered`, `Failed`, `CreatedAt`
- `AuditLogs`: `Id`, `UserId`, `Action`, `Module`, `Timestamp`

Production additions:

- `CampaignQueueItems`: durable per-contact send queue with `CampaignId`, `ContactId`, `Status`, `AttemptCount`, `NextAttemptAt`, `LockedAt`, `LockedBy`, `LastError`, `CreatedAt`, and `ProcessedAt`.
- `WebhookEvents`: raw webhook storage with `CampaignId`, `ContactId`, `MessageId`, `EventType`, `PayloadJson`, `ReceivedAt`, and `ProcessedAt`.
- `ClientBrandingSettings`: default/admin branding separate from client branding where required.
- `UploadedMedia`: uploaded file metadata, content type, size, owner client, local path if retained, and JioCX returned public URL.

Model rules:

- Admin users may have `ClientId = null` and can view all clients.
- Manager and Viewer users must have `ClientId` and are scoped to one client.
- `Clients.ApiKey` is encrypted at rest.
- `Users.PasswordHash` uses ASP.NET Core password hashing.
- `CampaignMessages.PayloadJson` stores the validated JioCX-compatible message payload.
- Contacts are capped at 50 per campaign upload.
- `MessageLogs` are append-only history.
- Current status also lives on contacts and queue rows for fast dashboard queries.
- Reports are stored snapshots but can be regenerated from logs.

## Authentication, RBAC, And Security

Authentication is hybrid:

- Cookie authentication for MVC page sessions.
- JWT generation after login for AJAX/API-style calls where useful.
- No public registration page.
- Admin users create all users.
- Login page is shared by Admin, Manager, and Viewer.

Default roles:

- `Admin`: full access to all clients, onboarding, user management, reports, downloads, audit logs, and default branding.
- `Manager`: client-scoped campaign, upload, send, dashboard, and reports according to assigned permissions.
- `Viewer`: client-scoped dashboard and reports/download access only.

Permissions:

- `View`
- `Add`
- `Update`
- `Delete`
- `Download`

Endpoint authorization is module-aware, for example `Campaigns:Add`, `Campaigns:View`, `Reports:Download`, `Clients:Add`, and `Users:Update`.

RBAC enforcement happens in three places:

- Middleware/filter level: each protected request checks authentication and required permission.
- Service level: each use case enforces tenant/client scope.
- UI level: Razor helpers hide menus and disable buttons based on permissions.

Security rules:

- API keys are encrypted using ASP.NET Core Data Protection before storage.
- Passwords are hashed with `IPasswordHasher`.
- Uploaded media is validated for allowed MIME type and size before sending to JioCX.
- Rich card URLs must be HTTPS.
- CSV contacts are validated and capped at 50.
- Forms and AJAX requests use anti-forgery tokens.
- Audit logs capture login, client onboarding, user creation, campaign creation, queueing sends, report download, and permission-sensitive changes.

Error handling:

- `401`: clear session and redirect to login.
- `403`: show not-allowed page or return a structured AJAX not-allowed response.
- `429`: retry through the queue with backoff.
- `500`: retry through the queue with capped attempts.
- `400` and `404`: log as failed without retry unless the failure is clearly transient.

## JioCX Integration

`uploadFile` is used by the message builder for images, videos, and GIFs. The request uses:

- header `x-apikey`
- multipart field `file`
- form field `agentId`

The returned public URL is stored in `UploadedMedia` and used in message payload JSON.

`sendMessage` is used only by the background queue processor. The request uses:

- header `Content-Type: application/json`
- header `x-apikey`
- body fields `messageID`, `agentID`, `contacts`, and `data`

Although the API example accepts multiple contacts, the queue processor sends one contact per queue item. This makes retries, per-contact logs, delivery updates, and reports accurate.

`checkCapability` is available as a one-number-at-a-time validation helper. The request uses:

- header `x-apikey`
- header `agentid`
- header `Content-Type: application/json`
- body field `PhoneNumbers`

## Campaign Flow

1. User creates a campaign.
2. User uploads or enters up to 50 contacts.
3. User builds a plain text or rich card message.
4. Application validates message and contact constraints.
5. Campaign is saved as draft, scheduled, or recurring.
6. When due, the application inserts per-contact rows into `CampaignQueueItems`.
7. Hosted background service locks pending queue rows.
8. Queue processor calls JioCX `sendMessage`.
9. Response is stored in `MessageLogs`.
10. Contact, queue, report, and dashboard stats are updated.
11. SignalR pushes updates to active dashboards and campaign detail screens.

The queue checks credits before sending. If credits are zero, send/upload/campaign actions are disabled in the UI, server-side services reject new send actions, and queue items are paused or failed with a clear "No credits available" status. Report downloads remain allowed.

## Message Builder

Supported sendable message types:

- Plain text using the documented `content.plainText` payload.
- Single rich card with image/video/GIF using the documented `content.richCardDetails.standalone` payload.
- Open URL CTA using the documented `openUrl` payload shape.

Validation rules:

- Contact is required.
- Maximum 50 contacts.
- Message type must be valid.
- HTTPS URLs only.
- Maximum 4 CTAs.
- Standalone image must be less than 2 MB.
- Standalone video must be less than 10 MB.
- Thumbnail must be less than 40 KB where used.
- Rich card title must be less than 80 characters.
- Rich card description must be less than 2000 characters.
- Supported image formats: `jpeg`, `jpg`, `gif`, `png`.
- Supported video formats: `mp4`, `mpeg`, `mpeg4`, `webm`.

The UI may model dialer, calendar, and location CTA draft controls because they are part of the requested panel feature set, but these actions must not be sent to JioCX until the exact documented payload schema is provided. The validator must block sending these action types with a clear message instead of inventing JSON.

## Webhooks

The web app exposes a webhook endpoint to receive delivery and user interaction updates. Because the PDF describes webhook capabilities but does not provide an exact request schema, the endpoint must:

- Accept and store the raw payload in `WebhookEvents`.
- Parse known fields defensively when present.
- Resolve campaign/contact/message where possible.
- Update `MessageLogs` and contact status when data can be mapped safely.
- Push SignalR updates for delivery, open, and click events when data is mapped safely.
- Preserve unknown fields in the raw JSON for audit and future parser updates.

No undocumented webhook schema will be assumed.

## UI And Realtime

The Web project will use Razor views, jQuery, AJAX, and SignalR.

Main areas:

- Login
- Dashboard
- Clients/Agents
- User Management
- Campaigns
- Message Builder
- Media Uploads
- Reports
- Audit Logs
- Profile/Branding

Admin experience:

- Sees all clients and aggregate stats.
- Can onboard clients by entering Brand Name, Agent Name, Agent ID, API Key, logo, site name, and credits.
- Can create users.
- Can set default branding.
- Keeps access to onboarding and user management.

Manager experience:

- Sees only assigned client.
- Can manage campaigns and uploads if permissions allow.
- Cannot access onboarding or user management after client onboarding creates the manager user.

Viewer experience:

- Sees only assigned client dashboard and reports.
- Campaign, send, and upload actions are hidden or disabled according to permissions.

Realtime:

- `DashboardHub` broadcasts campaign stats, credit updates, sent/delivered/failed counts, and aggregate metrics.
- `CampaignHub` broadcasts per-campaign queue/contact/log status.
- Webhook updates broadcast through SignalR after processing.

The UI style is an enterprise operations panel: sidebar navigation, top branding bar, dense tables, clear status indicators, and dashboard charts focused on campaign health, credit usage, and delivery rates.

## Reports

Reports include:

- Mobile number
- Status
- User actions such as clicked/opened when webhook data provides those events
- Campaign totals
- Sent, delivered, failed counts

Exports:

- CSV generated server-side.
- PDF generated server-side from report data.

Report downloads are permission-protected and audited. Report downloads remain available even when client credits are zero.

## Production Readiness

The repository will include:

- SQL Server EF Core migrations.
- Seed data for roles, permissions, and the first admin account.
- Strongly typed options for JWT, JioCX UAT base URL, encryption/data protection, uploads, queue settings, and retry settings.
- Health checks for app and database.
- Centralized exception handling.
- Structured logging.
- Environment-specific configuration examples.
- Deployment instructions for IIS or Kestrel behind a reverse proxy.
- README with setup, migration, first login, and JioCX credential configuration.

Testing:

- Unit tests for payload validation, CSV validation, permissions, queue retry decisions, and application services.
- Integration tests for repositories, Unit of Work, authentication, controller authorization, and EF Core mappings.
- JioCX API client tests with mocked HTTP responses, not live UAT calls.
- Webhook tests using defensive sample payloads.
- Manual verification checklist for login, onboarding, campaign creation, media upload, queue processing, SignalR updates, credit lockout, report export, and permission UI behavior.

## Delivery Constraint

The finished project must be production-structured and feature-complete for the documented API surface. Features requiring undocumented JioCX payload details must be present only as safe UI/draft or raw-capture capabilities and must be blocked from live sends until the exact JioCX schema is provided.
