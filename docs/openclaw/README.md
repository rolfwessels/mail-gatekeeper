# Mail Gatekeeper × OpenClaw Integration

This directory contains an **OpenClaw skill** for integrating Mail Gatekeeper with your personal AI assistant.

## What It Does

- **Monitors** your Mail Gatekeeper alerts automatically
- **Filters** marketing/spam without bothering you
- **Drafts** email replies when you ask
- **Tracks state** locally so you don't get duplicate notifications
- **Proactive alerting** via OpenClaw hooks (optional)

## Prerequisites

1. **Mail Gatekeeper** running and configured (see main README)
2. **OpenClaw** installed (https://openclaw.ai or https://github.com/openclaw/openclaw)
3. A Mail Gatekeeper API token

## Installation

### 1. Install the skill

```bash
# Option A: Copy to your workspace skills directory
mkdir -p ~/clawd/skills/mail-gatekeeper
cp SKILL.md ~/clawd/skills/mail-gatekeeper/
cp mail_gatekeeper_*.py ~/clawd/scripts/

# Option B: Symlink (if you want to track changes)
ln -s $(pwd) ~/clawd/skills/mail-gatekeeper
```

### 2. Configure authentication

Store your Mail Gatekeeper token in one of these locations:

```bash
# Workspace-specific (recommended)
echo "your-token-here" > ~/clawd/.mail-gatekeeper-token

# OR global fallback
echo "your-token-here" > ~/.mail-gatekeeper-token
```

Make sure the token file is readable:
```bash
chmod 600 ~/clawd/.mail-gatekeeper-token
```

### 3. Update the skill with your instance URL

Edit `SKILL.md` and update the URL if your Mail Gatekeeper is hosted elsewhere:

```markdown
- **URL:** `http://your-mail-gatekeeper-instance.com`
```

### 4. (Optional) Set up proactive hooks

If you want OpenClaw to proactively check your email and alert you:

1. Add a hook to your OpenClaw config that polls Mail Gatekeeper
2. Or use OpenClaw's cron feature to schedule periodic checks

Example cron job (checks every 2 hours):

```bash
openclaw cron add \
  --schedule "every:2h" \
  --session isolated \
  --agent-turn "Check mail-gatekeeper for new alerts using the mail-gatekeeper skill"
```

## Usage

Once installed, your OpenClaw agent will:

### Automatic filtering

Marketing emails, auto-replies, and newsletters are **silently ignored** based on detection patterns in the skill.

### Manual commands

Ask your agent naturally:

- "Check my email alerts"
- "Draft a reply to the [subject] email"
- "Ignore that email from [sender]"
- "Show me unhandled emails"

### Scripts

The skill includes two helper scripts for manual use or integration:

#### Check alerts
```bash
# Show only unhandled alerts
python3 ~/clawd/scripts/mail_gatekeeper_check.py

# Show all alerts (including handled)
python3 ~/clawd/scripts/mail_gatekeeper_check.py --include-handled
```

#### Mark alerts
```bash
# Mark as drafted
python3 ~/clawd/scripts/mail_gatekeeper_mark.py drafted <alertId>

# Mark as ignored
python3 ~/clawd/scripts/mail_gatekeeper_mark.py ignored <alertId>

# Mark as unhandled (undo)
python3 ~/clawd/scripts/mail_gatekeeper_mark.py unhandled <alertId>
```

## How It Works

### Local State Management

The skill maintains state in:
```
~/clawd/memory/mail-gatekeeper-state.json
```

This tracks which alerts have been:
- `drafted` — you created a reply
- `ignored` — you marked it as not needing action
- `unhandled` — default (will be shown)

This prevents duplicate notifications when Mail Gatekeeper re-surfaces the same alert.

### Auto-Filter Policy

The skill automatically ignores:

- **Marketing/newsletters** (JetBrains, SnapScan, etc.)
- **Auto-replies** (out-of-office messages)
- **Social media notifications**
- **Product updates**

Detection patterns:
- From: `marketing@`, `noreply@`, `hello@`, `team@`
- Servers: `marketo.org`, `mailchimp.com`, `sendgrid.net`
- Subjects: "Get the most out of", "New feature", "Newsletter"

You can customize the filter policy in `SKILL.md`.

### Draft Workflow

When you ask to draft a reply:

1. Agent fetches the alert from Mail Gatekeeper API
2. Composes a draft based on the email content
3. Creates the draft via `/v1/drafts` endpoint
4. Marks the alert as `drafted` in local state
5. Confirms the draft is ready in your Gmail/mail client

## Customization

### Adjust filter policy

Edit the "Auto-Filter Policy" section in `SKILL.md` to add/remove patterns.

### Change instance URL

Update the "Config" section in `SKILL.md`:

```markdown
- **URL:** `http://your-instance.com`
```

### Modify scripts

The Python scripts are simple wrappers around the Mail Gatekeeper API. Feel free to fork and extend them.

## Troubleshooting

### "Token not found" error

Make sure your token file exists and is readable:
```bash
ls -la ~/clawd/.mail-gatekeeper-token
cat ~/clawd/.mail-gatekeeper-token
```

### Agent doesn't filter marketing emails

Check the `SKILL.md` auto-filter patterns. The skill relies on from-address and subject-line heuristics.

### Duplicate notifications

The skill should prevent this via local state. If you're still seeing duplicates:

1. Check that `~/clawd/memory/mail-gatekeeper-state.json` exists
2. Verify the scripts are marking alerts correctly
3. Look for errors in OpenClaw logs

### Agent doesn't respond to email-related questions

Make sure:
1. The skill is installed in your workspace's `skills/` directory
2. OpenClaw can read the `SKILL.md` file
3. The skill's `description` matches your query (OpenClaw uses semantic matching)

## Contributing

Improvements welcome! Some ideas:

- [ ] Support for other email providers (not just Gmail)
- [ ] Smarter filtering (ML-based classification)
- [ ] Templated replies (common response patterns)
- [ ] Email summarization for long threads
- [ ] Integration with calendar (e.g., "schedule a meeting" in reply)

## License

Same as Mail Gatekeeper — see `LICENSE.txt` in the repo root.

---

**Questions?** Open an issue in the [mail-gatekeeper repo](https://github.com/rolfwessels/mail-gatekeeper).
