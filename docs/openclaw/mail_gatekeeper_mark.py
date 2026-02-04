#!/usr/bin/env python3
"""mail_gatekeeper_mark.py

Mark a Mail Gatekeeper alertId as handled in local state.

Examples:
  ./scripts/mail_gatekeeper_mark.py drafted <alertId>
  ./scripts/mail_gatekeeper_mark.py ignored <alertId>
  ./scripts/mail_gatekeeper_mark.py unhandled <alertId>   # remove local mark

State file:
  /home/node/clawd/memory/mail-gatekeeper-state.json
"""

import json
import os
import sys
from datetime import datetime, timezone

STATE_PATH = os.path.expanduser("/home/node/clawd/memory/mail-gatekeeper-state.json")


def now_iso():
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


def load_state():
    if not os.path.exists(STATE_PATH):
        return {"lastSinceIso": None, "draftedAlertIds": [], "ignoredAlertIds": [], "handled": {}, "lastScanAtIso": None}
    with open(STATE_PATH, "r", encoding="utf-8") as f:
        s = json.load(f)
    s.setdefault("draftedAlertIds", [])
    s.setdefault("ignoredAlertIds", [])
    s.setdefault("handled", {})
    return s


def save_state(state):
    os.makedirs(os.path.dirname(STATE_PATH), exist_ok=True)
    with open(STATE_PATH, "w", encoding="utf-8") as f:
        json.dump(state, f, indent=2, sort_keys=True)
        f.write("\n")


def main():
    if len(sys.argv) < 3:
        raise SystemExit("usage: mail_gatekeeper_mark.py <drafted|ignored|unhandled> <alertId>")
    status = sys.argv[1].strip().lower()
    aid = sys.argv[2].strip()
    if status not in ("drafted", "ignored", "unhandled"):
        raise SystemExit("status must be drafted|ignored|unhandled")

    state = load_state()
    handled = state.get("handled") or {}

    if status == "unhandled":
        handled.pop(aid, None)
        # keep legacy arrays in sync-ish (best effort)
        state["draftedAlertIds"] = [x for x in (state.get("draftedAlertIds") or []) if x != aid]
        state["ignoredAlertIds"] = [x for x in (state.get("ignoredAlertIds") or []) if x != aid]
    else:
        handled[aid] = {"status": status, "atIso": now_iso()}
        if status == "drafted":
            if aid not in state["draftedAlertIds"]:
                state["draftedAlertIds"].append(aid)
            state["ignoredAlertIds"] = [x for x in (state.get("ignoredAlertIds") or []) if x != aid]
        if status == "ignored":
            if aid not in state["ignoredAlertIds"]:
                state["ignoredAlertIds"].append(aid)
            state["draftedAlertIds"] = [x for x in (state.get("draftedAlertIds") or []) if x != aid]

    state["handled"] = handled
    state["lastScanAtIso"] = now_iso()
    save_state(state)
    print("ok")


if __name__ == "__main__":
    main()
