# Mail Gatekeeper

Minimal API that polls IMAP, classifies emails with simple rules, and exposes alerts. Can create draft replies (IMAP only — no sending).

## Quick Start

```bash
cd src
cp MailGatekeeper.Api/.env.example .env
# edit .env with your IMAP creds

docker build -f MailGatekeeper.Api/Dockerfile -t mail-gatekeeper:local .
docker run -d -p 8080:8080 --env-file .env --name mail-gatekeeper mail-gatekeeper:local
```

## API

All endpoints except `/health` require auth:

```
Authorization: Bearer <GATEKEEPER_API_TOKEN>
```

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check (no auth) |
| `/v1/alerts` | GET | List action_required alerts |
| `/v1/scan` | POST | Trigger manual scan |
| `/v1/drafts` | POST | Create draft reply |

### GET /v1/alerts

Query params:
- `limit` (int, default 20, max 200)

### POST /v1/drafts

```json
{
  "alertId": "<message-id>",
  "body": "Reply text here",
  "subjectPrefix": "Re: "  // optional
}
```

## Config (env vars)

| Variable | Default | Description |
|----------|---------|-------------|
| `GATEKEEPER_API_TOKEN` | — | Bearer token for API auth |
| `IMAP_HOST` | — | IMAP server hostname |
| `IMAP_PORT` | 993 | IMAP port |
| `IMAP_SSL` | true | Use SSL |
| `IMAP_USER` | — | IMAP username/email |
| `IMAP_PASS` | — | IMAP password/app password |
| `IMAP_INBOX` | INBOX | Inbox folder name |
| `IMAP_DRAFTS` | Drafts | Drafts folder name |
| `GATEKEEPER_CRON` | `0 * * * *` | Polling schedule (hourly) |
| `SCAN_ON_START` | false | Run scan immediately on startup |
| `SCAN_LIMIT` | 50 | Max messages to scan per run |
| `FETCH_BODY_SNIPPET` | false | Fetch message bodies for snippet |

## Classification Rules

Emails are classified as `action_required` if:
- Subject contains: action required, urgent, invoice, payment, overdue, confirm, verification, password reset, meeting, reschedule, sign, approve
- Body snippet contains a question mark (if `FETCH_BODY_SNIPPET=true`)

Ignored (classified as `info_only`):
- Sender contains: no-reply, noreply, donotreply
- Subject contains: newsletter, unsubscribe
