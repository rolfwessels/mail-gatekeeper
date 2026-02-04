---
name: mail-gatekeeper
description: Check email alerts and draft replies via Mail Gatekeeper API. Use when asked about emails, action-required messages, or drafting email replies. Also runs on schedule to proactively check for important emails.
---

# Mail Gatekeeper

Mail Gatekeeper (upstream) is intentionally *dumb*: it may surface the same "action_required" alert repeatedly.

**Therefore, THIS agent must keep local state** to avoid re-asking the user after an alert has been handled.

Local state file:
- `/home/node/clawd/memory/mail-gatekeeper-state.json`

## Config

- **URL:** `http://mail-gatekeeper.me.sels.co.za`
- **Token:** Read from `/home/node/clawd/.mail-gatekeeper-token` (preferred). Fallback: `~/.mail-gatekeeper-token`
- **Auth:** `Authorization: Bearer <token>`

## Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/v1/alerts` | GET | List action-required emails |
| `/v1/scan` | POST | Trigger manual inbox scan |
| `/v1/drafts` | POST | Create draft reply |

## The state problem (what was wrong)

The hook message "Mail Gatekeeper found X action-required email" is **not stateful**. It will re-notify for the same underlying email.

What was missing on our side:
- We sometimes created drafts **without consistently marking the alertId as handled**.
- When a repeated hook came in, we treated it as a new request instead of **reconciling against local handled-state**.

Fix: always reconcile incoming hook notifications against `/v1/alerts` + local state, and only prompt when something is **unhandled**.

## Local handling rules (must follow)

Define local status per alertId:
- `unhandled` (default)
- `drafted` (action taken)
- `ignored` (user said ignore/clear)

**Rule:** If an alert is `drafted` or `ignored`, do NOT ask the user about it again.

### When hook says "found 1 action-required email…"
1) Call `/v1/alerts?since=<2h ago>` (or 24h if uncertain)
2) Filter out alerts whose local status != `unhandled`
3) If nothing remains: stay quiet / respond "already handled" (depending on channel norms)
4) If some remain: summarize and ask what to do

## Scripts (preferred)

### List ONLY unhandled alerts (default)
```bash
python3 /home/node/clawd/scripts/mail_gatekeeper_check.py
```

### List all alerts (including drafted/ignored)
```bash
python3 /home/node/clawd/scripts/mail_gatekeeper_check.py --include-handled
```

### Mark an alert as handled
```bash
# mark drafted
python3 /home/node/clawd/scripts/mail_gatekeeper_mark.py drafted <alertId>

# mark ignored
python3 /home/node/clawd/scripts/mail_gatekeeper_mark.py ignored <alertId>

# undo mark
python3 /home/node/clawd/scripts/mail_gatekeeper_mark.py unhandled <alertId>
```

## Create Draft Reply (and update state)

Workflow when the user says “draft”:
1) Fetch the alertId from `/v1/alerts`
2) Call `/v1/drafts` to create the draft
3) If successful, mark local state as `drafted` for that alertId
4) Confirm draft created + ask for any details needed (e.g., phone number)

(If draft creation fails, do NOT mark drafted.)

## Manual curl examples

### Check alerts
```bash
TOKEN=$(cat /home/node/clawd/.mail-gatekeeper-token 2>/dev/null | tr -d '\n')
if [ -z "$TOKEN" ]; then TOKEN=$(cat ~/.mail-gatekeeper-token 2>/dev/null | tr -d '\n'); fi
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://mail-gatekeeper.me.sels.co.za/v1/alerts?limit=20&since=$(date -u -d '24 hours ago' +%Y-%m-%dT%H:%M:%SZ)"
```

### Create draft
```bash
curl -s -X POST \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"alertId":"<message-id>","body":"Reply text here","subjectPrefix":"Re: "}' \
  http://mail-gatekeeper.me.sels.co.za/v1/drafts
```

## Auto-Filter Policy (Silent Ignore)

Handle these **WITHOUT alerting the user**:

- **Marketing/newsletters** from known companies (JetBrains, SnapScan, Glif, etc.)
- **Auto-replies** (out-of-office, vacation messages)
- **Social media notifications** (LinkedIn, Twitter, etc.)
- **Product updates** and feature announcements
- Anything with "unsubscribe" links at the bottom

### Detection patterns:
- From addresses containing: `marketing@`, `noreply@`, `hello@`, `team@`
- Subjects with: "Get the most out of", "New feature", "Update:", "Newsletter"
- Mail servers: `marketo.org`, `mailchimp.com`, `sendgrid.net`

## Always Alert

DO alert for:

- **Action required** from real people (maintenance, property management, business contacts)
- **Direct questions** from individuals (not automated)
- **Financial** (invoices, statements, payments)
- **Legal/compliance** matters
- **Personal** messages from known contacts

## When Uncertain

If it looks borderline, **err on the side of silence** — the user prefers fewer interruptions. They can always check the mail gatekeeper web UI directly if needed.

---

*Policy change: User requested automatic filtering of marketing emails (2026-02-04)*
