# InCleanHome Profile Service
> User profile management microservice for InCleanHome.

This service owns the `ClientProfile` and `WorkerProfile` aggregates. It is responsible for:

- Creating client and worker profiles after the user is created in IAM Service.
- Updating profile data (name, phone, photo, worker-specific fields).
- Listing and filtering workers by service type, zone, gender, rating, etc.
- Updating worker statistics (`AverageRating`, `TotalServices`) when a service completes.

This service is part of the [InCleanHome platform](https://github.com/UPC-pre-SI657-2610-7943-Grupo3/incleanhome-platform). 

## Architecture in one paragraph
Standard DDD layering: Domain (aggregates, value objects, commands, queries,
repository contracts) → Application (command/query services) → Infrastructure
(EF Core persistence, JWT validation middleware) → Interfaces (REST controllers
+ DTOs). The service reads its non-sensitive config from **Consul KV** at startup
and falls back to `appsettings.json`. The database connection string and JWT
signing key come from environment variables.

Unlike the IAM Service, this service does **not** own the User entity — it
trusts the JWT issued by IAM (validates signature + extracts `userId` and `role`
from claims). It does **not** call IAM for every request; that would be too chatty.

## Folder layout (Clean Architecture)
```
src/InCleanHome.ProfileService/
├── Program.cs                              # composition root
├── appsettings.json                        # fallback config
│
├── Configuration/                          # Consul config loader (same as IAM)
├── Discovery/                              # Consul service registration (same as IAM)
│
├── Domain/
│   ├── Model/
│   │   ├── Aggregates/    (ClientProfile, WorkerProfile)
│   │   ├── ValueObjects/  (Gender)
│   │   ├── Commands/      (CreateClient, CreateWorker, Update*, RegisterCompletedService, ...)
│   │   └── Queries/       (GetByUserId, SearchWorkers, ...)
│   ├── Repositories/      (IClientProfileRepository, IWorkerProfileRepository, IUnitOfWork)
│   └── Services/          (Command + Query service interfaces)
│
├── Application/
│   └── Internal/
│       ├── CommandServices/  (ClientProfileCommandService, WorkerProfileCommandService)
│       └── QueryServices/    (ClientProfileQueryService, WorkerProfileQueryService)
│
├── Infrastructure/
│   ├── Persistence/
│   │   ├── ProfileDbContext.cs            # EF Core context (snake_case naming)
│   │   ├── BaseRepository.cs
│   │   ├── Repositories/                  # ClientProfileRepository, WorkerProfileRepository
│   │   └── Extensions/                    # ModelBuilder + StringExtensions
│   └── Pipeline/
│       └── JwtAuthMiddleware.cs           # validates JWT and extracts claims
│
└── Interfaces/
    └── REST/
        ├── Controllers/ProfilesController.cs
        ├── Resources/                     # DTOs (Create, Update, Output)
        └── Transform/                     # entity ↔ resource assemblers
```


The Profile Service runs on port `5002` internally. Reachable from outside through
the API Gateway at `http://localhost:8080`.

## API endpoints
All paths shown as they appear through the API Gateway:
| Method | Path | Purpose | Auth |
|---|---|---|---|
| POST  | `/api/v1/profiles/clients` | Create a client profile | Bearer JWT |
| POST  | `/api/v1/profiles/workers` | Create a worker profile | Bearer JWT |
| GET   | `/api/v1/profiles/me` | Get current user's profile (client OR worker) | Bearer JWT |
| PATCH | `/api/v1/profiles/me/client` | Update current client profile | Bearer JWT (client) |
| PATCH | `/api/v1/profiles/me/worker` | Update current worker profile | Bearer JWT (worker) |
| POST  | `/api/v1/profiles/me/photo` | Set current user's profile photo | Bearer JWT |
| GET   | `/api/v1/profiles/clients/{userId}` | Get a client's public profile | Bearer JWT |
| GET   | `/api/v1/profiles/workers/{userId}` | Get a worker's public profile | Bearer JWT |
| GET   | `/api/v1/profiles/workers` | List/search workers (filters in query string) | Bearer JWT |
| POST  | `/api/v1/profiles/workers/{userId}/completed-service` | Increment worker stats | Bearer JWT (admin) |

### Search query parameters
`GET /api/v1/profiles/workers?serviceType=cleaning&zone=miraflores&minRating=4.0`

Supported filters:
- `serviceType` — string (matches if worker's `ServiceTypes` contains this value)
- `zone` — string (matches if worker's `Zones` contains this value)
- `gender` — `female | male | other`
- `minAge`, `maxAge` — integers
- `maxHourlyRate` — decimal
- `minRating` — decimal (0–5)

Results are ordered by `AverageRating` descending.

## Database
This service owns the `profile_db` PostgreSQL database (running on port `5433`
in docker-compose to avoid clashing with `iam-db` on 5432).

Tables (snake_case):
- `client_profiles`
- `worker_profiles` (with PostgreSQL `text[]` columns for `service_types` and `zones`)

Schema is created via `Database.EnsureCreatedAsync()` on startup (same pattern as IAM).

## How this service relates to IAM
The Profile Service does not own the `User` entity, but every `ClientProfile`
and `WorkerProfile` has a `UserId` foreign-reference to a user that lives in
IAM's database. There is **no enforced foreign key constraint** between the two
databases (microservices own their schemas independently). Consistency is
maintained at the application layer: the frontend creates a user in IAM, gets
the user id, and then creates the matching profile here passing that id.

If a user is deleted in IAM, this service won't automatically know. A future
iteration with RabbitMQ will subscribe to `UserDeleted` events to clean up
orphan profiles.

## License
For academic use - InCleanHome team.
