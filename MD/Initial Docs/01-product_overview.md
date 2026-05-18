# Prompt for Claude — Product Overview

You are a senior product architect.

Build a clear product definition for a **Cab Booking & Roster Management Web App**.

## Context

This is NOT a ride-hailing app.

It is an internal enterprise system where:

* Employees book cab drops (currently only 11 PM)
* Booking is allowed only until a configurable freeze time
* After freeze, a final roster is generated
* Transport team handles cab assignment outside the system

## Core Requirements

* Login via mobile number (OTP)
* Admin approval required before usage
* Booking allowed only for next day
* Only one booking per user per day
* Freeze time is configurable
* After freeze → bookings locked
* Admin exports roster (CSV/Excel)

## Output Required

1. Clear product definition
2. User personas (Admin, Employee)
3. User journeys
4. System boundaries (what is included vs excluded)
5. Future extensibility ideas

Keep it concise but structured.
