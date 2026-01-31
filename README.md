# Mail Gatekeeper

Minimal API that polls IMAP, classifies emails with simple rules, and exposes alerts. Can create draft replies (IMAP only — no sending).

## Quick Start

### Using Docker Compose (Recommended)

```bash
# Clone and navigate to the project
cd mail-gatekeeper

# Edit docker-compose.yml or create .env file with your settings
docker compose up -d
```

### Using Makefile

```bash
# Build and start development container
make build up

# Run tests
make test

# Build for production
make env=prod publish

# Push to Docker Hub
make env=prod docker-build docker-push
```

### Manual Docker Build

```bash
cd src
docker build -f MailGatekeeper.Api/Dockerfile -t mail-gatekeeper:local .
docker run -d -p 8080:8080 \
  -e ImapUsername=your@email.com \
  -e ImapPassword=your-app-password \
  -e GatekeeperApiToken=your-secret-token \
  --name mail-gatekeeper mail-gatekeeper:local
```

## API

All endpoints except `/health` require auth:

```
Authorization: Bearer <GatekeeperApiToken>
```

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check (no auth) |
| `/v1/alerts` | GET | List action_required and replied_thread alerts |
| `/v1/scan` | POST | Trigger manual scan |
| `/v1/drafts` | POST | Create draft reply |

### GET /v1/alerts

Query params:
- `limit` (int, default 20, max 200) - Maximum number of alerts to return
- `since` (DateTimeOffset, optional) - Filter alerts received after this date

Example: `/v1/alerts?limit=50&since=2026-01-01T00:00:00Z`

### POST /v1/drafts

Creates a draft reply to an alert. Uses Reply-All logic (includes all To/Cc recipients except yourself).

```json
{
  "alertId": "<message-id>",
  "body": "Reply text here",
  "subjectPrefix": "Re: "  // optional, defaults to "Re: "
}
```

Response:
```json
{
  "draftMessageId": "...",
  "draftsFolder": "[Gmail]/Drafts",
  "inReplyTo": "..."
}
```

## Config (Environment Variables)

| Variable | Default | Description |
|----------|---------|-------------|
| **API Settings** | | |
| `GatekeeperApiToken` | `asdlkjfaslkdjsadfasdfasd` | Bearer token for API auth |
| **IMAP Settings** | | |
| `ImapHost` | `imap.gmail.com` | IMAP server hostname |
| `ImapPort` | `993` | IMAP port |
| `ImapUseSsl` | `true` | Use SSL/TLS |
| `ImapUsername` | (required) | IMAP username/email |
| `ImapPassword` | (required) | IMAP password ([Gmail App Password](https://myaccount.google.com/apppasswords)) |
| `ImapInboxFolder` | `INBOX` | Inbox folder name |
| `ImapDraftsFolder` | `[Gmail]/Drafts` | Drafts folder name (Gmail: `[Gmail]/Drafts`, Outlook: `Drafts`) |
| **Polling Settings** | | |
| `GatekeeperCron` | `0 * * * *` | Polling schedule in cron format (default: hourly) |
| `ScanOnStart` | `true` | Run scan immediately on startup |
| **Scan Settings** | | |
| `ScanLimit` | `50` | Max messages to scan per run |
| `FetchBodySnippet` | `true` | Fetch message body snippets (280 chars) |
| `FetchFullBody` | `false` | Fetch full message body instead of snippet |
| `DeduplicateThreads` | `true` | Group alerts by thread |
| `ThreadItemLimit` | `6` | Max items per thread when deduplicating |
| `IncludeRepliedThreads` | `true` | Include threads where you've already replied |
| **Rule Engine Settings** | | |
| `IgnoreSenderPatterns` | `no-reply,noreply,donotreply,info,mongodb.com,team.mongodb.com` | Comma-separated patterns to ignore in sender |
| `IgnoreSubjectPatterns` | `newsletter,unsubscribe,no-reply,noreply,do not reply` | Comma-separated patterns to ignore in subject |
| `ActionSubjectPatterns` | `action required,urgent,invoice,payment,overdue,confirm,meeting,reschedule,sign document,signature required,approve,maintenance` | Comma-separated patterns for action-required detection |

## Classification Rules

### `action_required`
Emails are classified as `action_required` if:
- Subject contains any of: `action required`, `urgent`, `invoice`, `payment`, `overdue`, `confirm`, `meeting`, `reschedule`, `sign document`, `signature required`, `approve`, `maintenance`
- Body snippet contains a question mark `?` (only if `FetchBodySnippet=true`)

### `replied_thread`
Emails are classified as `replied_thread` if:
- `IncludeRepliedThreads=true` (default)
- You have previously replied in the email thread
- The email doesn't match `action_required` rules

### `info_only` (Ignored)
Emails are ignored if:
- Sender contains: `no-reply`, `noreply`, `donotreply`, `info`, `mongodb.com`, `team.mongodb.com`
- Subject contains: `newsletter`, `unsubscribe`, `no-reply`, `noreply`, `do not reply`

## Makefile Commands

| Command | Description |
|---------|-------------|
| `make help` | Show all available commands |
| `make up` | Start Docker Compose containers |
| `make down` | Stop Docker Compose containers |
| `make build` | Rebuild Docker Compose containers |
| `make test` | Run all tests with coverage |
| `make publish` | Build release binaries (linux-x64, win-x64) |
| `make docker-build` | Build Docker image with tags |
| `make docker-push` | Push Docker image to registry |
| `make docker-clean` | Clean up local Docker images |
| `make env=dev publish` | Build debug version |
| `make env=prod docker-publish` | Build and push production image |

## Development

Built with:
- .NET 9.0
- ASP.NET Core Minimal APIs
- MailKit for IMAP
- Cronos for scheduling
- Swagger/OpenAPI

### Project Structure
```
mail-gatekeeper/
├── src/
│   └── MailGatekeeper.Api/
│       ├── Imap/           # IMAP client & service
│       ├── Rules/          # Email classification engine
│       ├── Program.cs      # API endpoints
│       └── Settings.cs     # Configuration
├── tests/
│   └── MailGatekeeper.Api.Tests/
│       └── Rules/          # Classification tests
├── docker-compose.yml      # Dev container setup
└── Makefile               # Build automation
```
