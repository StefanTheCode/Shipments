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

### 6. Logs Example

```
2026-02-06 15:39:53.902 +01:00 [INF] (Corr=) Outbox dispatcher started. InstanceId=DESKTOP-RFDQKR0:f7fc32c3, IntervalSeconds=2, BatchSize=50, LockSeconds=30 {"EventId":{"Id":8001,"Name":"Started"},"SourceContext":"Shipments.Infrastructure.Outbox.OutboxDispatcherHostedService","MachineName":"DESKTOP-RFDQKR0","ProcessId":29900,"ThreadId":8}
2026-02-06 15:40:32.963 +01:00 [INF] (Corr=dd69a025da5048e69cdb1f75c84295ee) Azure Blob container ensured. Container=shipments-documents {"EventId":{"Id":11000,"Name":"ContainerEnsured"},"SourceContext":"Shipments.Infrastructure.Storage.AzureBlobStorage","RequestId":"0HNJ5HMDSIJQ5:00000013","RequestPath":"/api/shipments","ConnectionId":"0HNJ5HMDSIJQ5","MachineName":"DESKTOP-RFDQKR0","ProcessId":29900,"ThreadId":23}
2026-02-06 15:40:32.979 +01:00 [INF] (Corr=dd69a025da5048e69cdb1f75c84295ee) Shipment create requested. ReferenceNumber=REF-5432, Sender=Stefan, Recipient=Marko {"EventId":{"Id":1000,"Name":"ShipmentCreateRequested"},"SourceContext":"Shipments.Application.Abstraction.IShipmentService","RequestId":"0HNJ5HMDSIJQ5:00000013","RequestPath":"/api/shipments","ConnectionId":"0HNJ5HMDSIJQ5","MachineName":"DESKTOP-RFDQKR0","ProcessId":29900,"ThreadId":23}
2026-02-06 15:40:33.194 +01:00 [INF] (Corr=dd69a025da5048e69cdb1f75c84295ee) Shipment created successfully. ShipmentId="64dc0f31-f67c-4fc8-86ac-d8a48433cc22", ReferenceNumber=REF-5432 {"EventId":{"Id":1003,"Name":"ShipmentCreated"},"SourceContext":"Shipments.Application.Abstraction.IShipmentService","RequestId":"0HNJ5HMDSIJQ5:00000013","RequestPath":"/api/shipments","ConnectionId":"0HNJ5HMDSIJQ5","MachineName":"DESKTOP-RFDQKR0","ProcessId":29900,"ThreadId":23}
2026-02-06 15:40:55.954 +01:00 [INF] (Corr=4ba90a4fe8f44d20b050cf0efef2dd75) Document upload started. ShipmentId="64dc0f31-f67c-4fc8-86ac-d8a48433cc22", FileName=1757866942774.jpg, Size=113119 {"EventId":{"Id":2000,"Name":"DocumentUploadStarted"},"SourceContext":"Shipments.Application.Abstraction.IShipmentService","RequestId":"0HNJ5HMDSIJQ5:00000015","RequestPath":"/api/shipments/64dc0f31-f67c-4fc8-86ac-d8a48433cc22/documents","ConnectionId":"0HNJ5HMDSIJQ5","MachineName":"DESKTOP-RFDQKR0","ProcessId":29900,"ThreadId":20}
2026-02-06 15:40:58.880 +01:00 [INF] (Corr=4ba90a4fe8f44d20b050cf0efef2dd75) Blob upload succeeded. BlobName=64dc0f31-f67c-4fc8-86ac-d8a48433cc22/a4159a36-f8e6-4937-a123-3d1a187d4019-1757866942774.jpg {"EventId":{"Id":11101,"Name":"UploadSucceeded"},"SourceContext":"Shipments.Infrastructure.Storage.AzureBlobStorage","ShipmentId":"64dc0f31-f67c-4fc8-86ac-d8a48433cc22","RequestId":"0HNJ5HMDSIJQ5:00000015","RequestPath":"/api/shipments/64dc0f31-f67c-4fc8-86ac-d8a48433cc22/documents","ConnectionId":"0HNJ5HMDSIJQ5","MachineName":"DESKTOP-RFDQKR0","ProcessId":29900,"ThreadId":23}
2026-02-06 15:41:00.240 +01:00 [INF] (Corr=4ba90a4fe8f44d20b050cf0efef2dd75) Document uploaded to storage. ShipmentId="64dc0f31-f67c-4fc8-86ac-d8a48433cc22", BlobName=64dc0f31-f67c-4fc8-86ac-d8a48433cc22/a4159a36-f8e6-4937-a123-3d1a187d4019-1757866942774.jpg {"EventId":{"Id":2004,"Name":"BlobUploaded"},"SourceContext":"Shipments.Application.Abstraction.IShipmentService","RequestId":"0HNJ5HMDSIJQ5:00000015","RequestPath":"/api/shipments/64dc0f31-f67c-4fc8-86ac-d8a48433cc22/documents","ConnectionId":"0HNJ5HMDSIJQ5","MachineName":"DESKTOP-RFDQKR0","ProcessId":29900,"ThreadId":23}
2026-02-06 15:41:03.157 +01:00 [INF] (Corr=4ba90a4fe8f44d20b050cf0efef2dd75) Shipment status updated to DocumentUploaded. ShipmentId="64dc0f31-f67c-4fc8-86ac-d8a48433cc22" {"EventId":{"Id":2005,"Name":"ShipmentStatusUpdated"},"SourceContext":"Shipments.Application.Abstraction.IShipmentService","RequestId":"0HNJ5HMDSIJQ5:00000015","RequestPath":"/api/shipments/64dc0f31-f67c-4fc8-86ac-d8a48433cc22/documents","ConnectionId":"0HNJ5HMDSIJQ5","MachineName":"DESKTOP-RFDQKR0","ProcessId":29900,"ThreadId":20}
2026-02-06 15:41:06.336 +01:00 [INF] (Corr=4ba90a4fe8f44d20b050cf0efef2dd75) Outbox message created. Type=DocumentUploaded, ShipmentId="64dc0f31-f67c-4fc8-86ac-d8a48433cc22", BlobName=64dc0f31-f67c-4fc8-86ac-d8a48433cc22/a4159a36-f8e6-4937-a123-3d1a187d4019-1757866942774.jpg {"EventId":{"Id":2007,"Name":"OutboxEnqueued"},"SourceContext":"Shipments.Application.Abstraction.IShipmentService","RequestId":"0HNJ5HMDSIJQ5:00000015","RequestPath":"/api/shipments/64dc0f31-f67c-4fc8-86ac-d8a48433cc22/documents","ConnectionId":"0HNJ5HMDSIJQ5","MachineName":"DESKTOP-RFDQKR0","ProcessId":29900,"ThreadId":24}
2026-02-06 15:41:09.366 +01:00 [INF] (Corr=4ba90a4fe8f44d20b050cf0efef2dd75) Publishing message to Azure Service Bus. ShipmentId="64dc0f31-f67c-4fc8-86ac-d8a48433cc22", CorrelationId=4ba90a4fe8f44d20b050cf0efef2dd75 {"EventId":{"Id":4000,"Name":"PublishStarted"},"SourceContext":"Shipments.Infrastructure.Messaging.AzureServiceBusPublisher","BlobName":"64dc0f31-f67c-4fc8-86ac-d8a48433cc22/a4159a36-f8e6-4937-a123-3d1a187d4019-1757866942774.jpg","OutboxId":"4c9ef7ed-b462-4549-a025-ac05f953742f","OutboxType":"DocumentUploaded","InstanceId":"DESKTOP-RFDQKR0:f7fc32c3","MachineName":"DESKTOP-RFDQKR0","ProcessId":29900,"ThreadId":23}
2026-02-06 15:41:45.406 +01:00 [INF] (Corr=4ba90a4fe8f44d20b050cf0efef2dd75) Message successfully published to Azure Service Bus. ShipmentId="64dc0f31-f67c-4fc8-86ac-d8a48433cc22" {"EventId":{"Id":4001,"Name":"PublishSucceeded"},"SourceContext":"Shipments.Infrastructure.Messaging.AzureServiceBusPublisher","BlobName":"64dc0f31-f67c-4fc8-86ac-d8a48433cc22/a4159a36-f8e6-4937-a123-3d1a187d4019-1757866942774.jpg","OutboxId":"4c9ef7ed-b462-4549-a025-ac05f953742f","OutboxType":"DocumentUploaded","InstanceId":"DESKTOP-RFDQKR0:f7fc32c3","MachineName":"DESKTOP-RFDQKR0","ProcessId":29900,"ThreadId":24}
2026-02-06 15:41:45.406 +01:00 [INF] (Corr=4ba90a4fe8f44d20b050cf0efef2dd75) Outbox message dispatched. OutboxId="4c9ef7ed-b462-4549-a025-ac05f953742f", Type=DocumentUploaded {"EventId":{"Id":8201,"Name":"MessageDispatched"},"SourceContext":"Shipments.Infrastructure.Outbox.OutboxDispatcherHostedService","OutboxType":"DocumentUploaded","InstanceId":"DESKTOP-RFDQKR0:f7fc32c3","MachineName":"DESKTOP-RFDQKR0","ProcessId":29900,"ThreadId":24}
2026-02-06 15:49:57.926 +01:00 [INF] (Corr=) Outbox dispatcher started. InstanceId=DESKTOP-RFDQKR0:f078ec1d, IntervalSeconds=2, BatchSize=50, LockSeconds=30 {"EventId":{"Id":8001,"Name":"Started"},"SourceContext":"Shipments.Infrastructure.Outbox.OutboxDispatcherHostedService","MachineName":"DESKTOP-RFDQKR0","ProcessId":21916,"ThreadId":11}
2026-02-06 15:50:13.449 +01:00 [INF] (Corr=661f25703b8c4974a6806656af51b3c2) Azure Blob container ensured. Container=shipments-documents {"EventId":{"Id":11000,"Name":"ContainerEnsured"},"SourceContext":"Shipments.Infrastructure.Storage.AzureBlobStorage","RequestId":"0HNJ5HS46TQ78:00000001","RequestPath":"/api/shipments","ConnectionId":"0HNJ5HS46TQ78","MachineName":"DESKTOP-RFDQKR0","ProcessId":21916,"ThreadId":14}
2026-02-06 15:50:13.470 +01:00 [INF] (Corr=661f25703b8c4974a6806656af51b3c2) Shipment create requested. ReferenceNumber=REF-5432, Sender=Stefan, Recipient=Marko {"EventId":{"Id":1000,"Name":"ShipmentCreateRequested"},"SourceContext":"Shipments.Application.Abstraction.IShipmentService","RequestId":"0HNJ5HS46TQ78:00000001","RequestPath":"/api/shipments","ConnectionId":"0HNJ5HS46TQ78","MachineName":"DESKTOP-RFDQKR0","ProcessId":21916,"ThreadId":14}
2026-02-06 15:50:13.750 +01:00 [INF] (Corr=661f25703b8c4974a6806656af51b3c2) Shipment created successfully. ShipmentId="6348a9e2-5073-4e28-b477-3ea59676f050", ReferenceNumber=REF-5432 {"EventId":{"Id":1003,"Name":"ShipmentCreated"},"SourceContext":"Shipments.Application.Abstraction.IShipmentService","RequestId":"0HNJ5HS46TQ78:00000001","RequestPath":"/api/shipments","ConnectionId":"0HNJ5HS46TQ78","MachineName":"DESKTOP-RFDQKR0","ProcessId":21916,"ThreadId":14}
2026-02-06 15:50:42.262 +01:00 [INF] (Corr=4abc1c01bd444081a299d82bd00bd4cf) Document upload started. ShipmentId="6348a9e2-5073-4e28-b477-3ea59676f050", FileName=HAVbPUsXwAATg6F.jpg, Size=58942 {"EventId":{"Id":2000,"Name":"DocumentUploadStarted"},"SourceContext":"Shipments.Application.Abstraction.IShipmentService","RequestId":"0HNJ5HS46TQ78:00000003","RequestPath":"/api/shipments/6348a9e2-5073-4e28-b477-3ea59676f050/documents","ConnectionId":"0HNJ5HS46TQ78","MachineName":"DESKTOP-RFDQKR0","ProcessId":21916,"ThreadId":13}
2026-02-06 15:50:54.698 +01:00 [INF] (Corr=4abc1c01bd444081a299d82bd00bd4cf) Blob upload succeeded. BlobName=6348a9e2-5073-4e28-b477-3ea59676f050/7b207639-7077-4202-8729-59fdb13b557f-HAVbPUsXwAATg6F.jpg {"EventId":{"Id":11101,"Name":"UploadSucceeded"},"SourceContext":"Shipments.Infrastructure.Storage.AzureBlobStorage","ShipmentId":"6348a9e2-5073-4e28-b477-3ea59676f050","RequestId":"0HNJ5HS46TQ78:00000003","RequestPath":"/api/shipments/6348a9e2-5073-4e28-b477-3ea59676f050/documents","ConnectionId":"0HNJ5HS46TQ78","MachineName":"DESKTOP-RFDQKR0","ProcessId":21916,"ThreadId":19}
2026-02-06 15:51:00.069 +01:00 [INF] (Corr=4abc1c01bd444081a299d82bd00bd4cf) Document uploaded to storage. ShipmentId="6348a9e2-5073-4e28-b477-3ea59676f050", BlobName=6348a9e2-5073-4e28-b477-3ea59676f050/7b207639-7077-4202-8729-59fdb13b557f-HAVbPUsXwAATg6F.jpg {"EventId":{"Id":2004,"Name":"BlobUploaded"},"SourceContext":"Shipments.Application.Abstraction.IShipmentService","RequestId":"0HNJ5HS46TQ78:00000003","RequestPath":"/api/shipments/6348a9e2-5073-4e28-b477-3ea59676f050/documents","ConnectionId":"0HNJ5HS46TQ78","MachineName":"DESKTOP-RFDQKR0","ProcessId":21916,"ThreadId":19}
2026-02-06 15:51:07.260 +01:00 [INF] (Corr=4abc1c01bd444081a299d82bd00bd4cf) Shipment status updated to DocumentUploaded. ShipmentId="6348a9e2-5073-4e28-b477-3ea59676f050" {"EventId":{"Id":2005,"Name":"ShipmentStatusUpdated"},"SourceContext":"Shipments.Application.Abstraction.IShipmentService","RequestId":"0HNJ5HS46TQ78:00000003","RequestPath":"/api/shipments/6348a9e2-5073-4e28-b477-3ea59676f050/documents","ConnectionId":"0HNJ5HS46TQ78","MachineName":"DESKTOP-RFDQKR0","ProcessId":21916,"ThreadId":11}
2026-02-06 15:51:11.731 +01:00 [INF] (Corr=4abc1c01bd444081a299d82bd00bd4cf) Outbox message created. Type=DocumentUploaded, ShipmentId="6348a9e2-5073-4e28-b477-3ea59676f050", BlobName=6348a9e2-5073-4e28-b477-3ea59676f050/7b207639-7077-4202-8729-59fdb13b557f-HAVbPUsXwAATg6F.jpg {"EventId":{"Id":2007,"Name":"OutboxEnqueued"},"SourceContext":"Shipments.Application.Abstraction.IShipmentService","RequestId":"0HNJ5HS46TQ78:00000003","RequestPath":"/api/shipments/6348a9e2-5073-4e28-b477-3ea59676f050/documents","ConnectionId":"0HNJ5HS46TQ78","MachineName":"DESKTOP-RFDQKR0","ProcessId":21916,"ThreadId":22}
2026-02-06 15:51:16.064 +01:00 [INF] (Corr=4abc1c01bd444081a299d82bd00bd4cf) Publishing message to Azure Service Bus. ShipmentId="6348a9e2-5073-4e28-b477-3ea59676f050", CorrelationId=4abc1c01bd444081a299d82bd00bd4cf {"EventId":{"Id":4000,"Name":"PublishStarted"},"SourceContext":"Shipments.Infrastructure.Messaging.AzureServiceBusPublisher","BlobName":"6348a9e2-5073-4e28-b477-3ea59676f050/7b207639-7077-4202-8729-59fdb13b557f-HAVbPUsXwAATg6F.jpg","OutboxId":"b4629580-4a34-4bf0-aa85-c56afa3a456a","OutboxType":"DocumentUploaded","InstanceId":"DESKTOP-RFDQKR0:f078ec1d","MachineName":"DESKTOP-RFDQKR0","ProcessId":21916,"ThreadId":22}
2026-02-06 15:51:34.684 +01:00 [INF] (Corr=4abc1c01bd444081a299d82bd00bd4cf) Message successfully published to Azure Service Bus. ShipmentId="6348a9e2-5073-4e28-b477-3ea59676f050" {"EventId":{"Id":4001,"Name":"PublishSucceeded"},"SourceContext":"Shipments.Infrastructure.Messaging.AzureServiceBusPublisher","BlobName":"6348a9e2-5073-4e28-b477-3ea59676f050/7b207639-7077-4202-8729-59fdb13b557f-HAVbPUsXwAATg6F.jpg","OutboxId":"b4629580-4a34-4bf0-aa85-c56afa3a456a","OutboxType":"DocumentUploaded","InstanceId":"DESKTOP-RFDQKR0:f078ec1d","MachineName":"DESKTOP-RFDQKR0","ProcessId":21916,"ThreadId":20}
2026-02-06 15:51:34.685 +01:00 [INF] (Corr=4abc1c01bd444081a299d82bd00bd4cf) Outbox message dispatched. OutboxId="b4629580-4a34-4bf0-aa85-c56afa3a456a", Type=DocumentUploaded {"EventId":{"Id":8201,"Name":"MessageDispatched"},"SourceContext":"Shipments.Infrastructure.Outbox.OutboxDispatcherHostedService","OutboxType":"DocumentUploaded","InstanceId":"DESKTOP-RFDQKR0:f078ec1d","MachineName":"DESKTOP-RFDQKR0","ProcessId":21916,"ThreadId":20}
```
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
