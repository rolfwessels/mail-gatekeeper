#!/usr/bin/env python3
"""mail_gatekeeper_check.py

Fetch action_required alerts from Mail Gatekeeper and annotate them with local state.

Local state lives in: /home/node/clawd/memory/mail-gatekeeper-state.json

We treat the upstream Mail Gatekeeper service as "dumb": it may announce the same
alert repeatedly. This script is the source of truth for whether we have already
*handled* an alert (drafted or ignored).

Usage:
  - Default: print only UNHANDLED alerts (json)
  - --include-handled: print all alerts
  - --since-hours N: override lookback window
"""

import argparse
import json
import os
from datetime import datetime, timezone, timedelta
import urllib.request

STATE_PATH = os.path.expanduser("/home/node/clawd/memory/mail-gatekeeper-state.json")
TOKEN_PATHS = [
    "/home/node/clawd/.mail-gatekeeper-token",
    os.path.expanduser("~/.mail-gatekeeper-token"),
]
BASE_URL = "http://mail-gatekeeper.me.sels.co.za"


def now_iso():
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


def load_state():
    if not os.path.exists(STATE_PATH):
        return {"lastSinceIso": None, "draftedAlertIds": [], "ignoredAlertIds": [], "handled": {}, "lastScanAtIso": None}
    with open(STATE_PATH, "r", encoding="utf-8") as f:
        state = json.load(f)
    # Back-compat defaults
    state.setdefault("draftedAlertIds", [])
    state.setdefault("ignoredAlertIds", [])
    state.setdefault("handled", {})
    state.setdefault("lastSinceIso", None)
    state.setdefault("lastScanAtIso", None)

    # Migrate list-based state into handled map (without deleting old keys)
    h = state.get("handled") or {}
    for aid in state.get("draftedAlertIds") or []:
        h.setdefault(aid, {"status": "drafted"})
    for aid in state.get("ignoredAlertIds") or []:
        h.setdefault(aid, {"status": "ignored"})
    state["handled"] = h
    return state


def save_state(state):
    os.makedirs(os.path.dirname(STATE_PATH), exist_ok=True)
    with open(STATE_PATH, "w", encoding="utf-8") as f:
        json.dump(state, f, indent=2, sort_keys=True)
        f.write("\n")


def read_token():
    for p in TOKEN_PATHS:
        try:
            with open(p, "r", encoding="utf-8") as f:
                t = f.read().strip()
                if t:
                    return t
        except FileNotFoundError:
            pass
    raise SystemExit(f"No token found in: {TOKEN_PATHS}")


def http_json(method, url, token, body=None):
    data = None
    headers = {"Authorization": f"Bearer {token}"}
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=20) as resp:
        raw = resp.read().decode("utf-8")
        return json.loads(raw) if raw else None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--include-handled", action="store_true", help="include drafted/ignored alerts in output")
    ap.add_argument("--since-hours", type=float, default=None, help="override lookback window")
    args = ap.parse_args()

    token = read_token()
    state = load_state()

    # Choose since:
    if args.since_hours is not None:
        since = datetime.now(timezone.utc) - timedelta(hours=float(args.since_hours))
    elif state.get("lastSinceIso"):
        since = datetime.fromisoformat(state["lastSinceIso"].replace("Z", "+00:00"))
    else:
        since = datetime.now(timezone.utc) - timedelta(hours=24)

    since_param = since.replace(microsecond=0).isoformat().replace("+00:00", "Z")
    alerts = http_json(
        "GET",
        f"{BASE_URL}/v1/alerts?limit=50&since={since_param}",
        token,
    ) or []

    handled = state.get("handled") or {}

    out = []
    newest_received = since
    for a in alerts:
        aid = a.get("id")
        received_at = a.get("receivedAt")
        try:
            ra = datetime.fromisoformat(received_at.replace("Z", "+00:00"))
        except Exception:
            ra = None
        if ra and ra > newest_received:
            newest_received = ra

        st = (handled.get(aid) or {}).get("status")
        a["_localStatus"] = st or "unhandled"

        if args.include_handled or a["_localStatus"] == "unhandled":
            out.append(a)

    # advance cursor to newest receivedAt (so we don't reprocess)
    state["lastSinceIso"] = newest_received.astimezone(timezone.utc).replace(microsecond=0).isoformat()
    state["lastScanAtIso"] = now_iso()
    save_state(state)

    print(
        json.dumps(
            {
                "sinceUsed": since_param,
                "alerts": out,
                "counts": {
                    "totalFetched": len(alerts),
                    "returned": len(out),
                },
                "state": state,
            },
            indent=2,
            sort_keys=True,
        )
    )


if __name__ == "__main__":
    main()
