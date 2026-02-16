"""
Real-time web dashboard for LEAN Research MCP server.

Runs as a daemon thread inside the MCP server process. Serves a single-page
dark-themed timeline that displays code executions, charts, outputs, and
errors as they stream in via Server-Sent Events (SSE).

Zero external dependencies — stdlib only.

Usage from mcp_server.py:
    import dashboard
    dashboard.start()              # launches HTTP server on port 5111
    dashboard.push_event("code_input", {"code": "print(1)"})
"""

from __future__ import annotations

import json
import queue
import socketserver
import threading
import time
import webbrowser
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, HTTPServer

# ---------------------------------------------------------------------------
# EventBus — thread-safe fan-out to SSE subscribers
# ---------------------------------------------------------------------------

_HISTORY_SIZE = 200


class EventBus:
    """Fan-out event dispatcher with history buffer for late-joining clients."""

    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._subscribers: list[queue.Queue] = []
        self._history: list[tuple[int, str, str]] = []  # (id, type, data_json)
        self._next_id = 1

    def subscribe(self, last_event_id: int = 0) -> queue.Queue:
        q: queue.Queue = queue.Queue(maxsize=512)
        with self._lock:
            # Replay missed events
            for eid, etype, edata in self._history:
                if eid > last_event_id:
                    q.put((eid, etype, edata))
            self._subscribers.append(q)
        return q

    def unsubscribe(self, q: queue.Queue) -> None:
        with self._lock:
            try:
                self._subscribers.remove(q)
            except ValueError:
                pass

    def push(self, event_type: str, data: dict) -> None:
        data_json = json.dumps(data, default=str)
        with self._lock:
            eid = self._next_id
            self._next_id += 1
            self._history.append((eid, event_type, data_json))
            # Trim history
            if len(self._history) > _HISTORY_SIZE:
                self._history = self._history[-_HISTORY_SIZE:]
            for q in list(self._subscribers):
                try:
                    q.put_nowait((eid, event_type, data_json))
                except queue.Full:
                    pass  # slow consumer, skip


_bus = EventBus()

# ---------------------------------------------------------------------------
# Embedded HTML page
# ---------------------------------------------------------------------------

_HTML_PAGE = r"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>LEAN Research Dashboard</title>
<style>
:root {
  --bg: #0d1117;
  --surface: #161b22;
  --border: #30363d;
  --text: #e6edf3;
  --text-muted: #8b949e;
  --blue: #58a6ff;
  --green: #3fb950;
  --red: #f85149;
  --purple: #bc8cff;
  --orange: #d29922;
  --gray: #8b949e;
}
* { box-sizing: border-box; margin: 0; padding: 0; }
body {
  background: var(--bg);
  color: var(--text);
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
  font-size: 14px;
  line-height: 1.5;
}
header {
  position: sticky; top: 0; z-index: 10;
  background: var(--surface);
  border-bottom: 1px solid var(--border);
  padding: 12px 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
}
header h1 {
  font-size: 16px;
  font-weight: 600;
}
#status {
  font-size: 12px;
  display: flex;
  align-items: center;
  gap: 6px;
}
#status .dot {
  width: 8px; height: 8px;
  border-radius: 50%;
  background: var(--gray);
}
#status.connected .dot { background: var(--green); }
#status.disconnected .dot { background: var(--red); }

#timeline {
  max-width: 900px;
  margin: 0 auto;
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

#empty-state {
  text-align: center;
  color: var(--text-muted);
  padding: 80px 20px;
  font-size: 15px;
}

.card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
  overflow: hidden;
}
.card-header {
  padding: 8px 14px;
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  border-bottom: 1px solid var(--border);
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.card-header .timestamp {
  font-weight: 400;
  text-transform: none;
  letter-spacing: 0;
  color: var(--text-muted);
}
.card-body {
  padding: 12px 14px;
}
.card-body pre {
  white-space: pre-wrap;
  word-break: break-word;
  font-family: 'SF Mono', 'Fira Code', 'Cascadia Code', monospace;
  font-size: 13px;
  line-height: 1.6;
}
.card-body img {
  max-width: 100%;
  border-radius: 4px;
  margin-top: 4px;
}

/* Type accents */
.card.code_input  { border-left: 3px solid var(--blue); }
.card.code_input  .card-header { color: var(--blue); }

.card.code_output { border-left: 3px solid var(--green); }
.card.code_output .card-header { color: var(--green); }

.card.code_error  { border-left: 3px solid var(--red); }
.card.code_error  .card-header { color: var(--red); }

.card.chart       { border-left: 3px solid var(--purple); }
.card.chart       .card-header { color: var(--purple); }

.card.html_display { border-left: 3px solid var(--orange); }
.card.html_display .card-header { color: var(--orange); }

.card.status, .card.kernel_restart, .card.kernel_status {
  border-left: 3px solid var(--gray);
}
.card.status .card-header,
.card.kernel_restart .card-header,
.card.kernel_status .card-header { color: var(--gray); }

.html-frame {
  width: 100%;
  border: none;
  background: #fff;
  border-radius: 4px;
  min-height: 60px;
}
</style>
</head>
<body>
<header>
  <h1>LEAN Research Dashboard</h1>
  <div id="status" class="disconnected">
    <span class="dot"></span>
    <span id="status-text">Connecting...</span>
  </div>
</header>
<div id="timeline">
  <div id="empty-state">Waiting for code executions...</div>
</div>
<script>
(function() {
  const timeline = document.getElementById('timeline');
  const emptyState = document.getElementById('empty-state');
  const statusEl = document.getElementById('status');
  const statusText = document.getElementById('status-text');
  let lastEventId = 0;
  let autoScroll = true;
  let cellCounter = 0;

  // Track scroll position
  window.addEventListener('scroll', function() {
    const atBottom = (window.innerHeight + window.scrollY) >= (document.body.scrollHeight - 50);
    autoScroll = atBottom;
  });

  function scrollToBottom() {
    if (autoScroll) {
      window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
    }
  }

  function timestamp() {
    return new Date().toLocaleTimeString();
  }

  function escapeHtml(s) {
    const d = document.createElement('div');
    d.textContent = s;
    return d.innerHTML;
  }

  function makeCard(type, headerText, bodyHtml) {
    emptyState.style.display = 'none';
    const card = document.createElement('div');
    card.className = 'card ' + type;
    card.innerHTML =
      '<div class="card-header"><span>' + escapeHtml(headerText) + '</span>' +
      '<span class="timestamp">' + timestamp() + '</span></div>' +
      '<div class="card-body">' + bodyHtml + '</div>';
    timeline.appendChild(card);
    scrollToBottom();
    return card;
  }

  function handleEvent(type, data) {
    switch (type) {
      case 'code_input':
        cellCounter++;
        makeCard('code_input', 'In [' + cellCounter + ']',
          '<pre>' + escapeHtml(data.code || '') + '</pre>');
        break;

      case 'code_output': {
        let parts = [];
        if (data.stdout) parts.push(escapeHtml(data.stdout));
        if (data.result) parts.push('<span style="color:var(--green)">=> ' + escapeHtml(data.result) + '</span>');
        if (parts.length > 0) {
          makeCard('code_output', 'Out [' + cellCounter + ']',
            '<pre>' + parts.join('\n') + '</pre>');
        }
        break;
      }

      case 'code_error':
        makeCard('code_error', 'Error',
          '<pre>' + escapeHtml(data.error || data.stderr || 'Unknown error') + '</pre>');
        break;

      case 'chart':
        makeCard('chart', 'Chart' + (data.index !== undefined ? ' ' + data.index : ''),
          '<img src="data:image/png;base64,' + data.png_base64 + '" alt="Chart">');
        break;

      case 'html_display':
        var card = makeCard('html_display', 'Display',
          '<iframe class="html-frame" sandbox="allow-same-origin"></iframe>');
        var iframe = card.querySelector('iframe');
        iframe.addEventListener('load', function() {
          try {
            var doc = iframe.contentDocument || iframe.contentWindow.document;
            doc.open();
            doc.write('<html><head><style>body{font-family:sans-serif;font-size:13px;margin:8px;color:#333;} table{border-collapse:collapse;width:100%;} th,td{border:1px solid #ddd;padding:6px 8px;text-align:left;} th{background:#f6f8fa;}</style></head><body>' + data.html + '</body></html>');
            doc.close();
            iframe.style.height = (doc.body.scrollHeight + 20) + 'px';
          } catch(e) {}
        });
        // Trigger load
        var doc = iframe.contentDocument || iframe.contentWindow.document;
        doc.open();
        doc.write('<html><head><style>body{font-family:sans-serif;font-size:13px;margin:8px;color:#333;} table{border-collapse:collapse;width:100%;} th,td{border:1px solid #ddd;padding:6px 8px;text-align:left;} th{background:#f6f8fa;}</style></head><body>' + data.html + '</body></html>');
        doc.close();
        setTimeout(function(){
          try { iframe.style.height = (doc.body.scrollHeight + 20) + 'px'; } catch(e){}
        }, 100);
        break;

      case 'status':
        makeCard('status', data.level || 'Status',
          '<pre>' + escapeHtml(data.message || '') + '</pre>');
        break;

      case 'kernel_restart':
        makeCard('kernel_restart', 'Kernel Restarted',
          '<pre>Market: ' + escapeHtml(data.label || data.market || '') + '</pre>');
        break;

      case 'kernel_status':
        var msg = data.running ? 'Running' : 'Not running';
        if (data.label) msg += ' — ' + data.label;
        if (data.container) msg += ' (' + data.container + ')';
        makeCard('kernel_status', 'Kernel Status',
          '<pre>' + escapeHtml(msg) + '</pre>');
        break;

      case 'ping':
        break;  // keepalive, ignore

      default:
        makeCard('status', type,
          '<pre>' + escapeHtml(JSON.stringify(data, null, 2)) + '</pre>');
    }
  }

  function connect() {
    var url = '/events';
    if (lastEventId > 0) url += '?last_event_id=' + lastEventId;
    var es = new EventSource(url);

    es.onopen = function() {
      statusEl.className = 'connected';
      statusText.textContent = 'Connected';
    };

    es.onmessage = function(e) {
      if (e.lastEventId) lastEventId = parseInt(e.lastEventId, 10);
      try {
        var payload = JSON.parse(e.data);
        handleEvent(payload.type, payload.data);
      } catch(err) {}
    };

    es.onerror = function() {
      statusEl.className = 'disconnected';
      statusText.textContent = 'Reconnecting...';
      es.close();
      setTimeout(connect, 2000);
    };
  }

  connect();
})();
</script>
</body>
</html>"""

# ---------------------------------------------------------------------------
# Threaded HTTP server (so SSE doesn't block HTML serving)
# ---------------------------------------------------------------------------


class _ThreadedHTTPServer(socketserver.ThreadingMixIn, HTTPServer):
    daemon_threads = True
    allow_reuse_address = True


class _DashboardHandler(BaseHTTPRequestHandler):
    """Serves the HTML page at / and SSE stream at /events."""

    def log_message(self, format, *args):
        # Suppress default stderr logging — would pollute MCP stdio
        pass

    def do_GET(self):
        if self.path == "/" or self.path == "/index.html":
            self._serve_html()
        elif self.path.startswith("/events"):
            self._serve_sse()
        else:
            self.send_error(HTTPStatus.NOT_FOUND)

    def _serve_html(self):
        body = _HTML_PAGE.encode("utf-8")
        self.send_response(HTTPStatus.OK)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-cache")
        self.end_headers()
        self.wfile.write(body)

    def _serve_sse(self):
        # Parse Last-Event-ID from header or query string
        last_id = 0
        header_id = self.headers.get("Last-Event-ID", "")
        if header_id.isdigit():
            last_id = int(header_id)
        # Also check query string (?last_event_id=N)
        if "?" in self.path:
            qs = self.path.split("?", 1)[1]
            for param in qs.split("&"):
                if param.startswith("last_event_id="):
                    val = param.split("=", 1)[1]
                    if val.isdigit():
                        last_id = int(val)

        self.send_response(HTTPStatus.OK)
        self.send_header("Content-Type", "text/event-stream")
        self.send_header("Cache-Control", "no-cache")
        self.send_header("Connection", "keep-alive")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()

        sub = _bus.subscribe(last_event_id=last_id)
        try:
            while True:
                try:
                    eid, etype, edata = sub.get(timeout=15)
                    payload = json.dumps({"type": etype, "data": json.loads(edata)})
                    self.wfile.write(f"id: {eid}\ndata: {payload}\n\n".encode())
                    self.wfile.flush()
                except queue.Empty:
                    # Keepalive ping
                    try:
                        self.wfile.write(b": keepalive\n\n")
                        self.wfile.flush()
                    except (BrokenPipeError, ConnectionResetError):
                        break
                except (BrokenPipeError, ConnectionResetError):
                    break
        finally:
            _bus.unsubscribe(sub)


# ---------------------------------------------------------------------------
# DashboardServer — lifecycle management
# ---------------------------------------------------------------------------

_server: _ThreadedHTTPServer | None = None
_port: int = 0


def start(port_start: int = 5111, port_end: int = 5130) -> int | None:
    """Start the dashboard HTTP server on a daemon thread.

    Tries ports from port_start to port_end. Auto-opens browser.
    Returns the port number, or None if all ports failed.
    """
    global _server, _port
    if _server is not None:
        return _port

    for port in range(port_start, port_end + 1):
        try:
            srv = _ThreadedHTTPServer(("127.0.0.1", port), _DashboardHandler)
            _server = srv
            _port = port
            t = threading.Thread(target=srv.serve_forever, daemon=True)
            t.start()
            # Open browser after a short delay
            threading.Timer(0.5, webbrowser.open, args=[f"http://127.0.0.1:{port}"]).start()
            return port
        except OSError:
            continue

    return None


def push_event(event_type: str, data: dict) -> None:
    """Push an event to all connected dashboard clients."""
    _bus.push(event_type, data)
