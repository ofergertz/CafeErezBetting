# ☕ CafeErezBetting

מערכת קיוסק הימורים ולוטו לקפה שכונתי — כולל חנות, סריקת ברקוד, ניהול לקוחות, ולוג פעולות.

## Architecture

```
┌─────────────────────────────────────────────┐
│                  Browser                    │
│          React + TypeScript (Vite)          │
│    TanStack Query · Zustand · react-i18n    │
└──────────────────┬──────────────────────────┘
                   │ HTTP/WebSocket
┌──────────────────▼──────────────────────────┐
│            nginx reverse proxy              │
│          (port 80 / 443 in prod)            │
└────────┬──────────────────────┬─────────────┘
         │ /api                 │ /hubs
┌────────▼─────────┐   ┌────────▼─────────────┐
│  .NET 8 Web API  │   │      SignalR Hubs     │
│  (C# / ASP.NET)  │   │  MatchesHub           │
└────────┬─────────┘   │  NotificationsHub     │
         │             └─────────────────────--┘
    ┌────▼────┐  ┌──────────┐
    │Postgres │  │  Redis   │
    │   15    │  │    7     │
    └─────────┘  └──────────┘
```

## Features

- ⚽ **ווינר / Winner** — הימורי ספורט בזמן אמת עם bet slip
- 🏆 **טוטו / Toto** — טפסי טוטו עם תמיכה ב-14 עמודות
- 🔮 **לוטו / Lotto** — 6/37 + מספר חזק
- 🎯 **צ'אנס / Chance** — 5/36
- 7️⃣ **777** — 7/70
- 🛍️ **חנות** — ניהול מוצרים + סריקת ברקוד
- 👥 **ניהול לקוחות** — לקוחות + חובות
- 🖥️ **Admin Kiosk** — מסך קיוסק בזמן אמת
- 📋 **Audit Logs** — לוג פעולות אדמין
- 📱 **Mobile Nav** — ניווט תחתון למובייל

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) + Docker Compose
- [Node.js 20+](https://nodejs.org/) (for local dev)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for local dev)

## Quick Start (Docker)

```bash
# 1. Clone
git clone https://github.com/yourorg/CafeErezBetting.git
cd CafeErezBetting

# 2. Configure environment
cp .env.example .env
# Edit .env with your secrets

# 3. Start all services
docker compose up -d

# 4. Open
# Frontend: http://localhost:5173
# Backend API: http://localhost:5000
# API Docs: http://localhost:5000/api/docs
```

## Dev Setup (Without Docker)

### Frontend

```bash
cd frontend
npm install
npm run dev
# Runs on http://localhost:5173
```

### Backend

```bash
cd backend
dotnet restore
dotnet run --project src/CafeErezBetting.API
# Runs on http://localhost:5000
```

## Production Deployment

```bash
# Build and start production stack
docker compose -f docker-compose.prod.yml up -d --build

# Or pull pre-built images
BACKEND_IMAGE=caferez-backend:1.0.0 \
FRONTEND_IMAGE=caferez-frontend:1.0.0 \
docker compose -f docker-compose.prod.yml up -d
```

## Environment Variables

| Variable | Default | Description |
|---|---|---|
| `POSTGRES_DB` | `cafe_erez_betting` | Database name |
| `POSTGRES_USER` | `postgres` | DB username |
| `POSTGRES_PASSWORD` | — | DB password **(required in prod)** |
| `REDIS_PASSWORD` | — | Redis password **(required in prod)** |
| `JWT_SECRET` | — | JWT signing key (min 32 chars) **(required)** |
| `JWT_EXPIRY_MINUTES` | `60` | Token expiry in minutes |
| `SMS_PROVIDER` | `inforu` | SMS provider (inforu) |
| `SMS_API_KEY` | — | SMS API key |
| `SMS_SENDER_ID` | `CafeErez` | SMS sender name |
| `FRONTEND_URL` | `http://localhost:5173` | CORS allowed origin |
| `VITE_API_URL` | `http://localhost:5000` | API base URL for frontend |
| `VITE_WS_URL` | `ws://localhost:5000` | WebSocket URL for frontend |

## Default Credentials (Dev Only)

> ⚠️ Change these immediately in production!

| Role | Username | Password |
|---|---|---|
| Admin | `admin` | `Admin1234!` |

## Barcode Scanner Setup

The store page supports USB/Bluetooth barcode scanners that emulate keyboard input.

1. Navigate to **חנות** (Store)
2. Click **📷 מצב סריקה** to enable scanner mode
3. Scan any product — the matching card will highlight and scroll into view
4. Scan an unknown barcode → toast "מוצר לא נמצא"

To add a barcode to a product: edit the product and fill in the **ברקוד** field.
Supported formats: EAN-13, Code128, and any alphanumeric string up to 50 characters.

## API Docs

Swagger UI available at: `http://localhost:5000/api/docs`

## Phase Roadmap

| Phase | Status | Description |
|---|---|---|
| 1 | ✅ | Project setup + Auth (JWT + OTP) |
| 2 | ✅ | Winner real-time + SignalR |
| 3 | ✅ | Lottery forms (Toto, Lotto, Chance, 777) |
| 4 | ✅ | Store (products CRUD) |
| 5 | ✅ | Customer management + debt tracking |
| 6 | ✅ | Admin kiosk screen |
| 7 | ✅ | Barcode scanner + Audit logs + Mobile nav + Docker prod |
| 8 | 🔜 | Payments integration |
| 9 | 🔜 | Reports & analytics |
