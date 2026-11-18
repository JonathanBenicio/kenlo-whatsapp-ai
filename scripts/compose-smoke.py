#!/usr/bin/env python3
"""Exercise the local-only Compose demo using synthetic data."""
import json
from urllib.request import Request, urlopen


def request(url, payload=None):
    data = None if payload is None else json.dumps(payload).encode()
    req = Request(url, data=data, headers={"Content-Type": "application/json"} if data else {})
    with urlopen(req, timeout=10) as response:
        return response.read()


assert b"<html" in request("http://127.0.0.1:5173/").lower()
assert json.loads(request("http://127.0.0.1:8080/health"))["mode"] == "simulation"
request("http://127.0.0.1:5678/healthz")
properties = json.loads(request("http://127.0.0.1:8080/api/properties?city=Campinas&maxPrice=700000&bedrooms=3"))
assert len(properties) == 1 and properties[0]["code"] == "DEMO-001"
conversation_id = "11111111-1111-1111-1111-111111111111"
payload = {"conversationId": conversation_id, "messageId": "compose-smoke", "text": "Olá, procuro um imóvel."}
url = "http://127.0.0.1:8080/api/simulation/messages"
first = json.loads(request(url, payload))
repeated = json.loads(request(url, payload))
assert len(first["messages"]) == len(repeated["messages"])
takeover = json.loads(request(f"http://127.0.0.1:8080/api/conversations/{conversation_id}/takeover", {}))
assert takeover["status"] == "human"
payload["messageId"] = "compose-smoke-human"
after = json.loads(request(url, payload))
assert after["status"] == "human" and len(after["messages"]) == len(takeover["messages"]) + 1
print("Compose: painel, API, n8n, filtros, deduplicação e transferência humana verificados.")
