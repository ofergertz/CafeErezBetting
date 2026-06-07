# ☕ CafeErezBetting

מערכת קיוסק הימורים ולוטו לקפה שכונתי.

## Features

- ⚽ **ווינר / Winner** — הימורי ספורט בזמן אמת עם bet slip
- 🏆 **טוטו / Toto** — טפסי טוטו עם תמיכה ב-14 עמודות
- 🔮 **לוטו / Lotto** — 6/37 + מספר חזק
- 🎯 **צ'אנס / Chance** — 5/36
- 7️⃣ **777** — 7/70
- 🛍️ **חנות** — ניהול מוצרים
- 👥 **ניהול לקוחות** — לקוחות + חובות
- 🖥️ **Admin Kiosk** — מסך קיוסק בזמן אמת

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | React + TypeScript + Vite |
| Styling | Tailwind CSS + CSS variables |
| State | Zustand + TanStack Query |
| i18n | react-i18next (HE/RU/EN + RTL/LTR) |
| Forms | React Hook Form + Zod |
| Backend | .NET Core 8 Web API (C#) |
| Real-time | SignalR |
| Database | PostgreSQL 15 |
| Cache | Redis 7 |
| Deployment | Docker Compose |
| CI/CD | GitHub Actions |

## Development Setup

### Prerequisites
- Docker + Docker Compose
- Node.js 20+
- .NET SDK 8

### Quick Start

```bash
# Clone
git clone https://github.com/ofergertz/CafeErezBetting.git
cd CafeErezBetting

# Start all services
docker compose up -d

# Frontend (dev)
cd frontend && npm install && npm run dev

# Backend (dev)
cd backend && dotnet run --project src/CafeErezBetting.API
```

### Environment Variables

Copy `.env.example` to `.env` and fill in:
```
POSTGRES_PASSWORD=your_password
REDIS_PASSWORD=your_password
JWT_SECRET=your_secret
SMS_API_KEY=your_sms_key
```

## API Documentation

After starting, visit: http://localhost:5000/api/docs (Swagger UI)

## Project Structure

```
CafeErezBetting/
├── frontend/          # React + TypeScript + Vite
├── backend/           # .NET Core 8 Web API
│   ├── src/
│   │   ├── CafeErezBetting.API          # Controllers, Hubs, Middleware
│   │   ├── CafeErezBetting.Core         # Entities, Interfaces, Services
│   │   └── CafeErezBetting.Infrastructure  # EF Core, Repositories, External
│   └── tests/
├── nginx/             # Reverse proxy config
├── .github/workflows/ # CI/CD
└── docs/              # OpenAPI spec
```

## Development Phases

| Phase | Status | Content |
|---|---|---|
| 1 — Foundation | 🚧 In Progress | Auth, routing, i18n, layout, RTL/LTR |
| 2 — Winner | ⏳ Planned | Live odds, bet slip, WebSocket notifications |
| 3 — Lottery | ⏳ Planned | Toto, Lotto, Chance, 777 |
| 4 — Admin | ⏳ Planned | Customer mgmt, debts, forms, dashboard |
| 5 — Store | ⏳ Planned | Store module (admin CRUD + customer view) |
| 6 — Polish | ⏳ Planned | Mobile, a11y, performance, CI/CD |

## Contributing

1. Never push directly to `main`
2. Branch: `feat/`, `fix/`, `chore/`
3. Open PR → wait for review + approval
