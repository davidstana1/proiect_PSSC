# ProiectPSSC

Sistem demo pentru **preluare comenzi**, **facturare** și **expediere**, implementat în .NET (straturi Domain/Application/Infrastructure/Api) cu **Postgres** și comunicare asincronă internă prin **Outbox pattern**.

## Workflows

1. **Preluare comandă**: API primește cerere, validează, persistă `Order` + eveniment `OrderPlaced` în outbox.
2. **Facturare**: worker-ul `OutboxDispatcher` procesează `OrderPlaced` și scrie `InvoiceCreated` în outbox (demo).
3. **Expediere**: procesează `InvoiceCreated` (demo).

## Rulare local

### 1) Pornește Postgres (Docker)

```bash
docker compose up -d
```

### 2) Pornește API

```bash
dotnet run --project src/ProiectPSSC.Api/ProiectPSSC.Api.csproj
```

API va porni cu Swagger în Development.

### 3) Plasează o comandă

```bash
curl -X POST http://localhost:5000/orders \
  -H 'Content-Type: application/json' \
  -d '{"customerEmail":"test@example.com","lines":[{"productCode":"P1","quantity":2,"unitPrice":10.5}]}'
```

## Notă despre migrații

În funcție de mediul tău Linux, rularea `dotnet-ef` poate necesita instalarea runtime-ului `Microsoft.AspNetCore.App`.
Deocamdată aplicația rulează și își creează schema prin `db.Database.Migrate()` dacă există migrații.

Următor pas: generăm migrațiile sau folosim `EnsureCreated()` în Development dacă runtime-ul lipsește.
