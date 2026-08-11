# File Vault - A File Sharing and Storage Platform

File Vault is a secure, multi-user file sharing and storage web application built with ASP.NET Core MVC. Users can register an account, upload and manage their own private files, and generate expiring, permission-controlled links to share individual files with anyone, no account required.

## Features

- **User accounts** - registration, login, and logout backed by ASP.NET Core Identity.
- **Private, per-user files** - every file is owned by the user who uploaded it and is only visible to them.
- **Database-backed metadata** - file name, type, size, upload date, and owner are tracked in SQL Server.
- **Drag-and-drop uploads** - a modern upload experience with live file selection feedback.
- **Shareable expiring links** - generate unguessable links to share a single file, with configurable expiry (1 hour to 30 days, or never) and an optional download limit.
- **Link permissions** - only a file's owner can create, list, or revoke its share links; links can be revoked at any time.
- **Password reset via email** - self-service reset flow using time-limited tokens delivered over SMTP.
- **Account lockout** - accounts lock for 15 minutes after 5 failed sign-in attempts to slow brute-force attacks.
- **Modern, responsive UI** - a clean custom design system layered on Bootstrap.

## Tech stack

| Area | Technology |
|---|---|
| Framework | ASP.NET Core 8 (MVC) |
| Language | C# (nullable + implicit usings enabled) |
| Auth | ASP.NET Core Identity |
| Data | Entity Framework Core 8 + SQL Server |
| Email | MailKit (SMTP) |
| Frontend | Razor views, Bootstrap, custom CSS/JS |

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server LocalDB (installed with Visual Studio, or via the SQL Server Express installer)
- Optionally, the EF Core CLI for managing migrations: `dotnet tool install --global dotnet-ef`

### Run the app

From the repository root:

```bash
dotnet run --project FileSharingandStorageSystem
```

On first launch the app automatically applies EF Core migrations and creates the `FileSharingDb` database in LocalDB. Then open the URL shown in the console (by default `https://localhost:7197` or `http://localhost:5254`), register an account, and start uploading.

## Configuration

### Database

The connection string lives in `appsettings.json` under `ConnectionStrings:DefaultConnection` and points at LocalDB by default. Update it to target a different SQL Server instance if needed.

### Email (password reset)

Email is configured through the `EmailSettings` section of `appsettings.json`. Non-secret defaults (host, port, sender) are kept there, while credentials should be supplied via user-secrets so they are never committed:

```bash
cd FileSharingandStorageSystem
dotnet user-secrets set "EmailSettings:Host" "smtp.example.com"
dotnet user-secrets set "EmailSettings:UserName" "you@example.com"
dotnet user-secrets set "EmailSettings:Password" "your-app-password"
dotnet user-secrets set "EmailSettings:FromEmail" "you@example.com"
```

If `EmailSettings:Host` is left empty (the default), the app runs in a development-friendly mode where reset links are written to the application log instead of being emailed, so the flow can be tested end to end without a real SMTP server.

For production, supply the same `EmailSettings:*` values via environment variables or a secrets manager rather than user-secrets.

## How it works

### File storage

Uploaded file bytes are stored on disk in a `Storage/` folder outside `wwwroot`, so they can never be fetched as static content, only through authorized controller actions. Each file is saved under a randomly generated name, while the original file name is preserved in the database. The `Storage/` folder is excluded from version control.

### Sharing and permissions

Creating a share generates a cryptographically random, URL-safe token stored in the `FileShares` table. Anyone with the resulting `/s/{token}` link can download the file while the link is active. A link is considered active only while it is not revoked, not past its expiry, and under its download limit. Ownership is enforced in the service layer, so a user can only manage shares for files they own.

## Project structure

```
FileSharingandStorageSystem/
  Controllers/        MVC controllers (Files, Account)
  Interfaces/         File storage and file share services
  Services/           Email sender (SMTP via MailKit)
  Models/             View models and settings
  Views/              Razor views (Files, Account, Shared)
  Migrations/         EF Core migrations
  wwwroot/            Static assets (CSS, JS, libraries)
  Storage/            User-uploaded files (git-ignored, created at runtime)
```

## Security notes

- Passwords are hashed by ASP.NET Core Identity (PBKDF2); plaintext is never stored.
- File access is scoped to the owning user; downloads require authorization.
- Share links use high-entropy tokens and support expiry, download caps, and revocation.
- Uploads are stored outside the web root and served only through validated endpoints.
- Anti-forgery tokens protect all state-changing form posts.
