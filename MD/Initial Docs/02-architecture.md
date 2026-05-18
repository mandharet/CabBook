# Prompt for Claude — System Architecture

You are a senior software architect.

Design architecture for a **multi-tenant cab booking web application**.

## Tech Stack

* Backend: ASP.NET Core Web API
* Frontend: React (Vite)
* Database: PostgreSQL
* Auth: OTP-based (SMS)
* Background jobs: Hangfire

## Requirements

* Multi-tenant (shared DB, TenantId column)
* Scalable and modular
* Clean separation of concerns
* Support future features (slots, pickup, notifications)

## Output Required

1. High-level architecture diagram (textual)
2. Layered architecture (Controller, Service, Repository)
3. Key components
4. Data flow (login, booking, freeze)
5. Background job design
6. Deployment considerations

Avoid over-engineering.
