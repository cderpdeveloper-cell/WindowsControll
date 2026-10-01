# Windows Control Center — Architecture Document

## Overview

Windows Control Center is a self-hosted centralized Windows computer management platform.
It allows administrators to manage multiple Windows PCs, laptops, servers, and Hyper-V VMs
from a single web dashboard.

## High-Level Architecture

```
Browser (React) → HTTPS/WSS → ASP.NET Core Server → HTTPS/WSS → Windows Agent → Windows APIs
```

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React 18+, TypeScript, Tailwind CSS, SignalR Client |
| Backend API | ASP.NET Core 8 Web API, C# |
| Real-time | SignalR |
| Database | SQL Server (EF Core) |
| Auth | JWT + Refresh Tokens, BCrypt, TOTP |
| Agent | .NET 8 Worker Service (Windows Service) |
| RDP | Apache Guacamole (guacd) via WebSocket gateway |

## Solution Structure

```
WindowsControlCenter/
├── backend/
│   ├── src/
│   │   ├── WindowsControlCenter.API/           # ASP.NET Core Web API
│   │   ├── WindowsControlCenter.Application/   # Services, DTOs, Validators
│   │   ├── WindowsControlCenter.Domain/        # Entities, Enums, Interfaces
│   │   └── WindowsControlCenter.Infrastructure/# EF Core, Repos, External Services
│   └── tests/
│       ├── WindowsControlCenter.UnitTests/
│       └── WindowsControlCenter.IntegrationTests/
│
├── agent/
│   ├── src/
│   │   ├── WindowsControlCenter.Agent/         # Windows Service host
│   │   └── WindowsControlCenter.Agent.Core/    # Agent logic, collectors
│   └── tests/
│       └── WindowsControlCenter.Agent.Tests/
│
├── frontend/
│   └── windows-control-center-web/             # React + Vite + TypeScript
│
├── deployment/
│   ├── docker/
│   ├── scripts/
│   └── installers/
│
└── docs/
```

## Clean Architecture Layers

1. **Domain** — Entities, enums, value objects, domain interfaces
2. **Application** — Use cases, DTOs, validators, service interfaces
3. **Infrastructure** — EF Core DbContext, repositories, external services
4. **API** — Controllers, middleware, SignalR hubs, DI configuration

## Database

- SQL Server with EF Core Code-First migrations
- All entities use GUID primary keys
- Soft delete via IsDeleted flag
- Audit columns: CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
- Multi-tenant via OrganizationId foreign key

## Authentication & Authorization

- JWT access tokens (short-lived, 15 min)
- Refresh tokens (long-lived, 7 days, rotated)
- BCrypt password hashing
- RBAC with granular permissions
- Agent authentication via AgentToken (API key + certificate)

## Agent Communication

- Agent initiates outbound SignalR connection (WebSocket)
- Server never needs inbound access to agent
- Heartbeat every 30 seconds
- Automatic reconnection with exponential backoff
- Agent identity: AgentId (GUID), MachineId, InstallationId, PublicKey

## RDP Architecture

- Apache Guacamole (guacd) as RDP protocol handler
- ASP.NET Core acts as Guacamole WebSocket gateway
- Browser renders desktop via guacamole-common-js
- No direct TCP 3389 exposure to internet

## Development Phases

1. Foundation (Auth, DB, Computers, Agents, SignalR, Dashboard)
2. Monitoring (CPU, RAM, Disk, Network, Alerts)
3. Windows Management (Processes, Services, Storage, Software)
4. Remote Desktop (Guacamole integration)
5. Administration (Files, IIS, DB, AD, Printers)
6. Automation (Scripts, Scheduler, Deployment)
7. Advanced (Backups, Policies, Snapshots, JIT, Vault)
8. Intelligence (Diagnostics, AI, Reporting)
