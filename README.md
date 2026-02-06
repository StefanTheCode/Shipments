# Shipments Processing Service

This project implements a shipment document processing system using **.NET**, **Clean Architecture principles**, **Result pattern**, **Outbox pattern**, and **asynchronous background processing**.

The system supports both **local development/testing** and **real-world cloud deployment (Azure)** with the same codebase, controlled by configuration flags.

---

## High-level Architecture

```
┌───────────────┐
│   HTTP API    │
│  (Carter)     │
└───────┬───────┘
        │
        │ Upload document
        ▼
┌────────────────────┐
│ Application Layer  │
│ - Use cases        │
│ - Result pattern   │
│ - CorrelationId    │
└────────┬───────────┘
         │
         │ DB transaction
         ▼
┌────────────────────┐
│ PostgreSQL         │
│ - Shipments        │
│ - Documents        │
│ - Outbox messages  │
└────────┬───────────┘
         │
         │ Outbox dispatch
         ▼
┌────────────────────┐
│ Infrastructure     │
│ - Blob storage     │
│ - Message bus      │
│ - Background svc   │
└────────┬───────────┘
         │
         │ Async processing
         ▼
┌────────────────────┐
│ Shipment processed │
└────────────────────┘
```

---

## Key Concepts

### Clean Architecture
- **Application** contains business use-cases and abstractions.
- **Infrastructure** contains integrations (DB, Blob, Message Bus, Background services).
- **API** only orchestrates HTTP requests.
- **Worker** (optional) hosts background services in production.

### Result Pattern
- No business exceptions.
- All expected failures return `Result.Fail(code, message)`.
- Infrastructure failures bubble up to workers for retry/DLQ handling.

### Correlation ID
- Every request is assigned a `X-Correlation-Id`.
- Propagated through:
  - HTTP
  - Outbox
  - Message bus
  - Background workers
- Included in **all logs** for traceability.

### Outbox Pattern
Guarantees **reliable message delivery**.

- Messages are written to DB in the same transaction as domain updates.
- A background dispatcher publishes them asynchronously.
- Prevents:
  - DB commit + message publish failure
  - Lost messages

---

## Runtime Modes

The entire behavior is controlled by **one flag**:

```json
"Runtime": {
  "UseAzure": false
}
```

| Mode | Blob Storage | Messaging | Background Worker |
|------|-------------|-----------|------------------|
| Local (`UseAzure=false`) | Local file system | In-memory queue | Hosted in API |
| Azure (`UseAzure=true`) | Azure Blob Storage | Azure Service Bus | Hosted in Worker |

---

## Project Structure

```
src/
├── Shipments.Api
│   ├── Carter endpoints
│   ├── Correlation middleware
│   └── Program.cs
│
├── Shipments.Application
│   ├── UseCases
│   ├── Abstractions
│   ├── Result pattern
│   └── DTO mappings
│
├── Shipments.Infrastructure
│   ├── Persistence (EF Core)
│   ├── Blob storage (Local/Azure)
│   ├── Messaging (InMemory / ServiceBus)
│   ├── Outbox
│   └── Background services
│
└── Shipments.Worker (optional)
    └── Hosts background services for Azure
```

---

## Processing Flow

### Upload document
1. HTTP request uploads file
2. Blob is stored
3. Shipment status updated
4. Outbox message created
5. DB transaction committed

### Outbox dispatcher
1. Periodically reads pending outbox messages
2. Publishes them to configured message bus
3. Marks them as dispatched
4. Retries on failure

### Background processing
1. Message received (local or Azure)
2. Blob downloaded
3. Shipment processed
4. Status updated to `Processed`
5. Idempotency enforced

---

## Idempotency

The system is **idempotent at shipment level**.

- If a shipment is already `Processed`, processing is skipped.
- Duplicate messages are safe.
- Supports at-least-once delivery.

---

## Logging

Logging is implemented using **Serilog** with file sinks.

### Log files
```
logs/
├── api/
│   ├── log-YYYYMMDD.txt
│   └── error-YYYYMMDD.txt
```

All logs include:
- CorrelationId
- ShipmentId (when applicable)
- Structured properties

---

## How to Run Locally

### 1. Prerequisites
- .NET SDK (latest)
- PostgreSQL
- Docker (optional)

### 2. Configure database
```json
"ConnectionStrings": {
  "ShipmentsDb": "Host=localhost;Port=5432;Database=shipments;Username=postgres;Password=postgres"
}
```

### 3. Apply migrations
```bash
dotnet ef database update \
  --project src/Shipments.Infrastructure \
  --startup-project src/Shipments.Api
```

### 4. Run API
```bash
dotnet run --project src/Shipments.Api
```

### 5. Test flow
1. Create shipment (`POST /shipments`)
2. Upload document (`POST /shipments/{id}/documents`)
3. Wait a few seconds
4. Check status (`GET /shipments/{id}` → `Processed`)
5. Inspect logs and outbox table

---

## Azure Deployment (Overview)

To enable Azure mode:

```json
"Runtime": {
  "UseAzure": true
}
```

Required services:
- Azure Blob Storage
- Azure Service Bus
- PostgreSQL (or Azure DB)

---

## Summary

This project demonstrates:
- Clean architecture
- Reliable async processing
- Safe message delivery
- Traceable distributed flow
- Real-world enterprise patterns
