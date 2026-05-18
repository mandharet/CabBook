# Prompt for Claude — Database Design

You are a senior backend engineer.

Design a production-ready database schema.

## Requirements

Entities:

* Tenant
* User
* Role
* Booking
* FreezeConfig
* ShiftConfig

## Rules

* Multi-tenant support (TenantId everywhere)
* One booking per user per day
* Booking only for next day
* Configurable freeze time

## Output Required

1. Table definitions (columns + types)
2. Primary & foreign keys
3. Indexing strategy
4. Constraints (unique, validation)
5. Sample SQL (PostgreSQL)

Keep it clean and normalized.
