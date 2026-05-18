# Prompt for Claude — Backend API Design

You are a senior .NET backend developer.

Design REST APIs for the system.

## Requirements

### Auth

* Send OTP
* Verify OTP

### User

* Get profile
* Admin approve/reject users

### Booking

* Create booking
* Get my booking
* Cancel booking

### Admin

* Get all bookings
* Set freeze time
* Enable/disable drop

## Rules

* Only next-day booking allowed
* Respect freeze time
* Prevent duplicate bookings

## Output Required

1. API endpoints
2. Request/response models
3. Validation rules
4. Error handling
5. Sample controller code (ASP.NET Core)

Keep APIs RESTful and clean.
