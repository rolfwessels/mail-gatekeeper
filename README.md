# Mail Gatekeeper

Minimal API that polls IMAP, classifies emails with simple rules, and exposes alerts. Can create draft replies (IMAP only — no sending).

## Quick Start To Develop

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
| `/health` | GET | Health check (no auth) - Returns status and version info |
| `/v1/alerts` | GET | List action_required and replied_thread alerts |
| `/v1/scan` | POST | Trigger manual scan |
| `/v1/drafts` | POST | Create draft reply |

### GET /health

Health check endpoint (no authentication required). Returns service status and version information.

Response:
```json
{
  "ok": true,
  "version": "0.1.123",
  "fileVersion": "0.1.123.0"
}
```

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

| Variable | Description |
|----------|-------------|
| **API Settings** | |
| `GatekeeperApiToken` | Bearer token for API auth <br> Default: `asdlkjfaslkdjsadfasdfasd` |
| **IMAP Settings** | |
| `ImapHost` | IMAP server hostname <br> Default: `imap.gmail.com` |
| `ImapPort` | IMAP port <br> Default: `993` |
| `ImapUseSsl` | Use SSL/TLS <br> Default: `true` |
| `ImapUsername` | IMAP username/email <br> **(required)** |
| `ImapPassword` | IMAP password ([Gmail App Password](https://myaccount.google.com/apppasswords)) <br> **(required)** |
| `ImapInboxFolder` | Inbox folder name <br> Default: `INBOX` |
| `ImapDraftsFolder` | Drafts folder name <br> Default: `[Gmail]/Drafts` (Gmail) or `Drafts` (Outlook) |
| **Polling Settings** | |
| `GatekeeperCron` | Polling schedule in cron format <br> Default: `0 * * * *` (hourly) |
| `ScanOnStart` | Run scan immediately on startup <br> Default: `true` |
| **Scan Settings** | |
| `ScanLimit` | Max messages to scan per run <br> Default: `50` |
| `FetchBodySnippet` | Fetch message body snippets (280 chars) <br> Default: `true` |
| `FetchFullBody` | Fetch full message body instead of snippet <br> Default: `false` |
| `DeduplicateThreads` | Group alerts by thread <br> Default: `true` |
| `ThreadItemLimit` | Max items per thread when deduplicating <br> Default: `6` |
| `IncludeRepliedThreads` | Include threads where you've already replied <br> Default: `true` |
| **Rule Engine Settings** | |
| `IgnoreSenderPatterns` | Comma-separated patterns to ignore in sender <br> Example: `no-reply,noreply,donotreply,info,mongodb.com,team.mongodb.com` |
| `IgnoreSubjectPatterns` | Comma-separated patterns to ignore in subject |
| `ActionSubjectPatterns` | Comma-separated patterns for action-required detection <br> Default: `action required,urgent,invoice,payment,overdue,confirm,meeting,reschedule,sign document,signature required,approve,maintenance` |
| **Webhook Settings** | |
| `WebhookUrl` | URL to POST notifications when new alerts are found <br> Default: *(empty)* |
| `WebhookToken` | Bearer token for webhook authentication (optional) <br> Default: *(empty)* |
| `WebhookMessage` | Custom message to send in webhook payload (OpenClaw integration) <br> Default: `You have new mail alerts, run the skill 'mail-gatekeeper' and let user know` |

### OpenClaw Integration

[OpenClaw](https://docs.openclaw.ai) can receive webhook notifications and inject them as events into your AI assistant session.

#### 1. Enable hooks in OpenClaw config

Add hooks to your `config.json`. See [OpenClaw Webhook Documentation](https://docs.openclaw.ai/automation/webhook) for full details.

```json
{
  "hooks": {
    "enabled": true,
    "token": "your-webhook-secret",
    "path": "/hooks"
  }
}
```

#### 2. Configure Mail Gatekeeper

Point the webhook URL to OpenClaw's `/hooks/agent` endpoint:

```bash
# Environment variables
WebhookUrl=https://your-openclaw-gateway.example.com/hooks/agent
WebhookToken=your-webhook-secret
WebhookMessage=You have new mail alerts, run the skill `mail-gatekeeper` and let user know
```

Or in `docker-compose.yml`:

```yaml
services:
  mail-gatekeeper:
    environment:
      - WebhookUrl=https://your-openclaw-gateway.example.com/hooks/agent
      - WebhookToken=${OPENCLAW_HOOK_TOKEN}
      - WebhookMessage=You have new mail alerts, run the skill `mail-gatekeeper` and let user know
```

**Why customize `WebhookMessage`?**

The `WebhookMessage` setting allows you to control what instruction OpenClaw receives when new mail alerts arrive. This is useful for:
- Telling OpenClaw to run a specific skill or command (e.g., `mail-gatekeeper`)
- Customizing the notification behavior (e.g., "Check mail silently" vs "Alert user immediately")
- Integrating with different OpenClaw workflows or automation scripts

The default message instructs OpenClaw to run the `mail-gatekeeper` skill and notify the user, but you can customize it to match your workflow needs.

#### 3. Test the integration

Trigger a manual scan to test:

```bash
curl -X POST http://localhost:8080/v1/scan \
  -H "Authorization: Bearer your-api-token"
```

If there are new action-required emails, OpenClaw will receive a webhook and inject the event into your session.

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
