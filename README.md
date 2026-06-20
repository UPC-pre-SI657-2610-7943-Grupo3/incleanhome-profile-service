# InCleanHome Profile Service

> User profile management microservice (ClientProfile + WorkerProfile).

Owns the `ClientProfile` and `WorkerProfile` aggregates. Handles:
- Profile creation called by IAM after Auth0 registration.
- Profile read/update (name, phone, photo, worker fields).
- Worker listing/search with filters (service type, zone, etc.).
- Worker stats updates triggered by `ReviewSubmitted` events.
- Orphan profile cleanup triggered by `UserDeleted` events.

## Endpoints

| Method | Path | Purpose | Auth |
|---|---|---|---|
| POST | `/api/v1/profiles/clients` | Create client profile | Bearer JWT |
| POST | `/api/v1/profiles/workers` | Create worker profile | Bearer JWT |
| GET | `/api/v1/profiles/me` | Get current profile (client OR worker) | Bearer JWT |
| PATCH | `/api/v1/profiles/me/client` | Update current client profile | Bearer JWT (client) |
| PATCH | `/api/v1/profiles/me/worker` | Update current worker profile | Bearer JWT (worker) |
| POST | `/api/v1/profiles/me/photo` | Set current user's profile photo | Bearer JWT |
| GET | `/api/v1/profiles/clients/{userId}` | Get a client's public profile | Bearer JWT |
| GET | `/api/v1/profiles/workers/{userId}` | Get a worker's public profile | Bearer JWT |
| GET | `/api/v1/profiles/workers` | List/search workers | Bearer JWT |

### Search query parameters

`GET /api/v1/profiles/workers?serviceTypes=limpieza,cuidado_ninos&zone=miraflores&minRating=4`

Supported filters:
- `serviceType` (singular) — match if worker offers this one.
- `serviceTypes` (plural, CSV) — AND: worker must offer **all** listed.
- `zone`, `gender`, `minAge`, `maxAge`, `maxHourlyRate`, `minRating`.

## Events

### Publishes (to `incleanhome.profile.events`)
- `WorkerProfileUpdatedEvent` — when worker profile or stats change.
- `ClientProfileUpdatedEvent` — when client profile changes.

### Consumes
- `ReviewSubmittedEvent` (from Reviews Service) → updates `AverageRating` + `TotalServices`.
- `UserDeletedEvent` (from IAM Service) → removes orphan profile.

## Environment variables

| Variable | Required | Purpose |
|---|---|---|
| `JWT_SIGNING_KEY` | YES | Same key the gateway and IAM use |
| `PROFILE_DB_CONNECTION` | YES | PostgreSQL connection string |
| `RABBITMQ_URL` | no | Format `amqps://user:pass@host/vhost`. Placeholder = no broker |
| `CONSUL_HTTP_ADDR` | no | Default `http://consul:8500` |

## Run

This service runs as part of the platform:
```bash
cd ../incleanhome-platform
docker compose up --build -d profile-service
```

Direct access: http://localhost:5002
Swagger UI (with "Authorize" button): http://localhost:5002/swagger

## Architecture notes

- **Database-per-service**: owns `profile_db`. No other service reads from it directly.
- **JWT validation**: extracts `userId` and `role` from claims. Does NOT lookup IAM
  on every request to avoid being chatty.
- **HTTP from IAM**: when IAM needs to resolve name/phone for `/auth/me` or
  `/auth0/login`, it calls this service over HTTP. This is intentional — keeps
  the frontend contract identical to the monolith.
- **Eventing**: MassTransit + RabbitMQ. Soft-fail if broker is unavailable.
