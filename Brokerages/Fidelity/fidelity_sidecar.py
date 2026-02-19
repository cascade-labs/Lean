"""
Fidelity Brokerage Sidecar Service

A FastAPI service that wraps the fidelity-api Playwright library to provide
a REST interface for the Lean FidelityBrokerage C# class.

Runs headless, fully automated with TOTP-based 2FA. No user interaction required.

Required environment variables:
    FIDELITY_USERNAME   - Fidelity account username
    FIDELITY_PASSWORD   - Fidelity account password
    FIDELITY_TOTP_SECRET - Base32-encoded TOTP secret for authenticator app
    FIDELITY_ACCOUNT    - BrokerageLink account number (Z-prefixed)
    FIDELITY_SIDECAR_PORT - Port to listen on (default: 5198)
"""

import os
import sys
import json
import time
import logging
import threading
from decimal import Decimal
from typing import Optional
from contextlib import asynccontextmanager

import uvicorn
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

from fidelity import fidelity as fid

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
log = logging.getLogger("fidelity_sidecar")

# ---------------------------------------------------------------------------
# Globals
# ---------------------------------------------------------------------------
bot: Optional[fid.FidelityAutomation] = None
bot_lock = threading.Lock()

FIDELITY_USERNAME = os.environ.get("FIDELITY_USERNAME", "")
FIDELITY_PASSWORD = os.environ.get("FIDELITY_PASSWORD", "")
FIDELITY_TOTP_SECRET = os.environ.get("FIDELITY_TOTP_SECRET", "")
FIDELITY_ACCOUNT = os.environ.get("FIDELITY_ACCOUNT", "")


# ---------------------------------------------------------------------------
# Request / Response models
# ---------------------------------------------------------------------------
class OrderRequest(BaseModel):
    symbol: str
    quantity: float
    action: str  # "buy" or "sell"
    account: Optional[str] = None
    dry_run: bool = False
    limit_price: Optional[float] = None


class OrderResponse(BaseModel):
    success: bool
    message: str
    order_id: Optional[str] = None


class HoldingItem(BaseModel):
    symbol: str
    quantity: float
    last_price: float
    value: float


class AccountInfo(BaseModel):
    account: str
    nickname: str
    balance: float
    holdings: list[HoldingItem]


class CashBalance(BaseModel):
    currency: str
    amount: float


# ---------------------------------------------------------------------------
# Bot lifecycle helpers
# ---------------------------------------------------------------------------
def _ensure_logged_in() -> fid.FidelityAutomation:
    """Return a logged-in FidelityAutomation instance, creating one if needed."""
    global bot
    with bot_lock:
        if bot is not None:
            return bot
        log.info("Starting headless Playwright browser for Fidelity...")
        bot = fid.FidelityAutomation(
            headless=True,
            title="lean_sidecar",
            save_state=True,
            profile_path="/tmp/fidelity_sessions",
        )
        step1, step2 = bot.login(
            FIDELITY_USERNAME,
            FIDELITY_PASSWORD,
            totp_secret=FIDELITY_TOTP_SECRET if FIDELITY_TOTP_SECRET else None,
        )
        if not step1:
            bot.close_browser()
            bot = None
            raise RuntimeError("Fidelity login failed (bad credentials or network issue)")
        if not step2:
            bot.close_browser()
            bot = None
            raise RuntimeError(
                "Fidelity login requires SMS 2FA. "
                "Configure a TOTP authenticator and set FIDELITY_TOTP_SECRET."
            )
        log.info("Fidelity login succeeded")
        return bot


def _shutdown_bot():
    global bot
    with bot_lock:
        if bot is not None:
            try:
                bot.close_browser()
            except Exception:
                pass
            bot = None


# ---------------------------------------------------------------------------
# FastAPI app
# ---------------------------------------------------------------------------
@asynccontextmanager
async def lifespan(app: FastAPI):
    # Startup: validate required env vars
    missing = []
    if not FIDELITY_USERNAME:
        missing.append("FIDELITY_USERNAME")
    if not FIDELITY_PASSWORD:
        missing.append("FIDELITY_PASSWORD")
    if missing:
        log.error(f"Missing required environment variables: {', '.join(missing)}")
        sys.exit(1)
    yield
    # Shutdown
    _shutdown_bot()


app = FastAPI(title="Fidelity Sidecar", lifespan=lifespan)


@app.get("/health")
def health():
    return {"status": "ok", "logged_in": bot is not None}


@app.post("/connect")
def connect():
    """Establish a session with Fidelity (login via Playwright)."""
    try:
        _ensure_logged_in()
        return {"success": True}
    except RuntimeError as e:
        raise HTTPException(status_code=503, detail=str(e))


@app.post("/disconnect")
def disconnect():
    """Close the browser session."""
    _shutdown_bot()
    return {"success": True}


@app.get("/accounts", response_model=list[AccountInfo])
def get_accounts():
    """Return all accounts and their positions."""
    b = _ensure_logged_in()
    with bot_lock:
        info = b.getAccountInfo()
    results = []
    for acct_num, data in info.items():
        holdings = []
        for s in data.get("stocks", []):
            holdings.append(HoldingItem(
                symbol=s["ticker"],
                quantity=float(s["quantity"]),
                last_price=float(s["last_price"]),
                value=float(s["value"]),
            ))
        results.append(AccountInfo(
            account=acct_num,
            nickname=data.get("nickname", ""),
            balance=float(data.get("balance", 0)),
            holdings=holdings,
        ))
    return results


@app.get("/holdings", response_model=list[HoldingItem])
def get_holdings():
    """Return holdings for the configured account (FIDELITY_ACCOUNT) or all."""
    b = _ensure_logged_in()
    with bot_lock:
        info = b.getAccountInfo()
    target = FIDELITY_ACCOUNT
    holdings = []
    for acct_num, data in info.items():
        if target and acct_num != target:
            continue
        for s in data.get("stocks", []):
            holdings.append(HoldingItem(
                symbol=s["ticker"],
                quantity=float(s["quantity"]),
                last_price=float(s["last_price"]),
                value=float(s["value"]),
            ))
    return holdings


@app.get("/cash", response_model=list[CashBalance])
def get_cash():
    """Return cash balances. Fidelity holds USD; core position (SPAXX/FDRXX) is cash."""
    b = _ensure_logged_in()
    with bot_lock:
        info = b.getAccountInfo()
    target = FIDELITY_ACCOUNT
    cash_usd = 0.0
    for acct_num, data in info.items():
        if target and acct_num != target:
            continue
        for s in data.get("stocks", []):
            ticker = s["ticker"].upper()
            # Core money market positions act as cash
            if ticker in ("SPAXX", "FDRXX", "FCASH", "FZFXX", "SPRXX"):
                cash_usd += float(s["value"])
    return [CashBalance(currency="USD", amount=cash_usd)]


@app.post("/order", response_model=OrderResponse)
def place_order(req: OrderRequest):
    """Place a buy/sell order."""
    b = _ensure_logged_in()
    account = req.account or FIDELITY_ACCOUNT
    if not account:
        raise HTTPException(status_code=400, detail="No account specified")

    action = req.action.lower()
    if action not in ("buy", "sell"):
        raise HTTPException(status_code=400, detail=f"Invalid action: {action}")

    with bot_lock:
        # Reload page between orders to avoid stale state
        b.page.reload()
        b.wait_for_loading_sign()
        success, err = b.transaction(
            stock=req.symbol.upper(),
            quantity=req.quantity,
            action=action,
            account=account,
            dry=req.dry_run,
            limit_price=req.limit_price,
        )

    if success:
        return OrderResponse(
            success=True,
            message=f"{'Preview' if req.dry_run else 'Order placed'}: "
                    f"{action} {req.quantity} {req.symbol.upper()}",
            order_id=f"FID-{int(time.time())}",
        )
    else:
        return OrderResponse(
            success=False,
            message=err or "Unknown error placing order",
        )


@app.post("/cancel")
def cancel_order():
    """Fidelity web UI doesn't support programmatic order cancel easily."""
    raise HTTPException(
        status_code=501,
        detail="Order cancellation not supported via Fidelity web automation",
    )


# ---------------------------------------------------------------------------
# Entrypoint
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    port = int(os.environ.get("FIDELITY_SIDECAR_PORT", "5198"))
    log.info(f"Starting Fidelity sidecar on port {port}")
    uvicorn.run(app, host="127.0.0.1", port=port, log_level="info")
