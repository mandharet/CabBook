# Corrected Database Schema

## Overview
Production-ready schema with all improvements: ShiftSlot management, locations, audit trails, state machines, and tenant isolation.

---

## Core Tables

### Tenant
Multi-tenant foundation with timezone and approval strategy.

```sql
CREATE TABLE Tenant (
    TenantId UUID PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    TimeZone VARCHAR(50) DEFAULT 'UTC',
    ApprovalRequirement VARCHAR(50) DEFAULT 'InviteOnly', 
    -- Options: None, DomainBased, InviteOnly, EmployeeId
    AllowedDomains VARCHAR(255),
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    UpdatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT tenant_name_unique UNIQUE(Name)
);

CREATE INDEX idx_tenant_active ON Tenant(IsActive);
```

---

### User
With explicit approval status and audit fields.

```sql
CREATE TABLE "User" (
    UserId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    Mobile VARCHAR(20) NOT NULL,
    Email VARCHAR(255),
    FullName VARCHAR(255) NOT NULL,
    IsApproved BOOLEAN DEFAULT FALSE,
    ApprovedAt TIMESTAMP WITH TIME ZONE,
    ApprovedBy UUID REFERENCES "User"(UserId),
    IsActive BOOLEAN DEFAULT TRUE,
    LastLoginAt TIMESTAMP WITH TIME ZONE,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    UpdatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT user_mobile_tenant_unique UNIQUE(TenantId, Mobile),
    CONSTRAINT user_email_unique UNIQUE(Email)
);

CREATE INDEX idx_user_tenant_approved ON "User"(TenantId, IsApproved);
CREATE INDEX idx_user_mobile ON "User"(Mobile);
```

---

### Role
Simple role management.

```sql
CREATE TABLE Role (
    RoleId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    Name VARCHAR(100) NOT NULL, -- Employee, Admin, SuperAdmin
    Permissions TEXT, -- JSON array of permission strings
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT role_name_tenant_unique UNIQUE(TenantId, Name)
);
```

---

### UserRole
Junction table for many-to-many relationship.

```sql
CREATE TABLE UserRole (
    UserRoleId UUID PRIMARY KEY,
    UserId UUID NOT NULL REFERENCES "User"(UserId) ON DELETE CASCADE,
    RoleId UUID NOT NULL REFERENCES Role(RoleId),
    AssignedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT userrole_unique UNIQUE(UserId, RoleId)
);

CREATE INDEX idx_userrole_user ON UserRole(UserId);
```

---

## Shift & Slot Management

### ShiftSlot
**KEY IMPROVEMENT:** Configurable shift slots with per-slot freeze time.

```sql
CREATE TABLE ShiftSlot (
    ShiftSlotId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    SlotName VARCHAR(100) NOT NULL, -- "7 PM Drop", "11 PM Drop"
    StartTime TIME NOT NULL, -- 23:00:00
    EndTime TIME NOT NULL, -- 23:30:00
    FreezeTime TIME NOT NULL, -- 20:00:00 (freeze happens at this time)
    DisplayOrder INT DEFAULT 1,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    UpdatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT shiftslot_name_tenant_unique UNIQUE(TenantId, SlotName),
    CONSTRAINT shiftslot_start_before_end CHECK(StartTime < EndTime)
);

CREATE INDEX idx_shiftslot_tenant_active ON ShiftSlot(TenantId, IsActive);
```

---

### ShiftFreezeOverride
Per-date freeze time overrides (e.g., holidays).

```sql
CREATE TABLE ShiftFreezeOverride (
    OverrideId UUID PRIMARY KEY,
    ShiftSlotId UUID NOT NULL REFERENCES ShiftSlot(ShiftSlotId),
    OverrideDate DATE NOT NULL,
    CustomFreezeTime TIME NOT NULL,
    Reason VARCHAR(255), -- "Extended for holiday", "Early maintenance"
    CreatedBy UUID NOT NULL REFERENCES "User"(UserId),
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT shiftfreeze_override_unique UNIQUE(ShiftSlotId, OverrideDate)
);

CREATE INDEX idx_shiftfreeze_slot_date ON ShiftFreezeOverride(ShiftSlotId, OverrideDate);
```

---

## Locations

### Location
**KEY IMPROVEMENT:** Predefined locations, not free text.

```sql
CREATE TABLE Location (
    LocationId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    Name VARCHAR(100) NOT NULL, -- "Powai", "Andheri", "Thane"
    Description TEXT,
    Latitude DECIMAL(10, 8),
    Longitude DECIMAL(11, 8),
    DisplayOrder INT DEFAULT 1,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT location_name_tenant_unique UNIQUE(TenantId, Name)
);

CREATE INDEX idx_location_tenant_active ON Location(TenantId, IsActive);
```

---

## Booking Management

### BookingStatusEnum
Status progression for a booking.

```sql
CREATE TYPE booking_status AS ENUM (
    'Draft',        -- Initial state, can edit/cancel
    'Confirmed',    -- Accepted, approaching freeze
    'Frozen',       -- Freeze passed, no changes allowed
    'Cancelled',    -- User cancelled before freeze
    'Rejected'      -- Admin rejected
);
```

---

### Booking
**KEY IMPROVEMENTS:** Links to ShiftSlot, Location, tracks status, allows edits before freeze.

```sql
CREATE TABLE Booking (
    BookingId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    UserId UUID NOT NULL REFERENCES "User"(UserId),
    ShiftSlotId UUID NOT NULL REFERENCES ShiftSlot(ShiftSlotId),
    LocationId UUID NOT NULL REFERENCES Location(LocationId),
    BookingDate DATE NOT NULL, -- The date for which booking is made
    Status booking_status DEFAULT 'Draft',
    
    -- Audit fields
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    UpdatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    CancelledAt TIMESTAMP WITH TIME ZONE,
    CancelledBy UUID REFERENCES "User"(UserId),
    
    -- Business rule: One booking per user per date per shift
    CONSTRAINT booking_user_date_shift_unique UNIQUE(TenantId, UserId, BookingDate, ShiftSlotId),
    CONSTRAINT booking_status_check CHECK(
        Status IN ('Draft', 'Confirmed', 'Frozen', 'Cancelled', 'Rejected')
    )
);

CREATE INDEX idx_booking_tenant_date ON Booking(TenantId, BookingDate);
CREATE INDEX idx_booking_user_date ON Booking(UserId, BookingDate);
CREATE INDEX idx_booking_status ON Booking(Status);
CREATE INDEX idx_booking_date_status ON Booking(BookingDate, Status);
```

---

### BookingAudit
**KEY IMPROVEMENT:** Full audit trail of all changes.

```sql
CREATE TABLE BookingAudit (
    AuditId UUID PRIMARY KEY,
    BookingId UUID NOT NULL REFERENCES Booking(BookingId) ON DELETE CASCADE,
    Action VARCHAR(50) NOT NULL, -- Created, Updated, Cancelled, Rejected, Restored
    
    -- What changed
    FieldName VARCHAR(100),
    OldValue TEXT,
    NewValue TEXT,
    
    -- Who and when
    ChangedBy UUID NOT NULL REFERENCES "User"(UserId),
    ChangedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    IPAddress VARCHAR(45), -- For security audit
    
    CONSTRAINT booking_audit_action_check CHECK(
        Action IN ('Created', 'Updated', 'Cancelled', 'Rejected', 'Restored')
    )
);

CREATE INDEX idx_booking_audit_booking ON BookingAudit(BookingId);
CREATE INDEX idx_booking_audit_changed_at ON BookingAudit(ChangedAt);
CREATE INDEX idx_booking_audit_action ON BookingAudit(Action);
```

---

## Roster Management

### RosterStatusEnum
Roster progression: Open → Frozen → Exported.

```sql
CREATE TYPE roster_status AS ENUM (
    'Open',      -- Bookings allowed
    'Frozen',    -- Freeze time passed, bookings locked
    'Exported'   -- Roster exported to transport team
);
```

---

### Roster
State machine per date per tenant.

```sql
CREATE TABLE Roster (
    RosterId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    RosterDate DATE NOT NULL,
    Status roster_status DEFAULT 'Open',
    
    -- When each state was reached
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    FrozenAt TIMESTAMP WITH TIME ZONE,
    ExportedAt TIMESTAMP WITH TIME ZONE,
    
    -- Track who froze/exported
    FrozenBy UUID REFERENCES "User"(UserId),
    ExportedBy UUID REFERENCES "User"(UserId),
    
    CONSTRAINT roster_tenant_date_unique UNIQUE(TenantId, RosterDate),
    CONSTRAINT roster_frozen_before_exported CHECK(
        (Status = 'Open' AND ExportedAt IS NULL AND FrozenAt IS NULL)
        OR (Status = 'Frozen' AND FrozenAt IS NOT NULL AND ExportedAt IS NULL)
        OR (Status = 'Exported' AND FrozenAt IS NOT NULL AND ExportedAt IS NOT NULL)
    )
);

CREATE INDEX idx_roster_tenant_date ON Roster(TenantId, RosterDate);
CREATE INDEX idx_roster_status ON Roster(Status);
```

---

### RosterSnapshot
**KEY IMPROVEMENT:** Immutable snapshot of frozen roster.

```sql
CREATE TABLE RosterSnapshot (
    SnapshotId UUID PRIMARY KEY,
    RosterId UUID NOT NULL REFERENCES Roster(RosterId),
    BookingId UUID NOT NULL REFERENCES Booking(BookingId),
    
    -- Denormalized data (snapshot doesn't follow updates)
    UserId UUID NOT NULL,
    UserName VARCHAR(255) NOT NULL,
    Mobile VARCHAR(20) NOT NULL,
    LocationName VARCHAR(100) NOT NULL,
    SlotName VARCHAR(100) NOT NULL,
    
    -- Metadata
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT rostersnapshot_unique UNIQUE(RosterId, BookingId)
);

CREATE INDEX idx_rostersnapshot_roster ON RosterSnapshot(RosterId);
```

---

## Configuration

### BookingConfig
Configurable booking rules per tenant.

```sql
CREATE TABLE BookingConfig (
    ConfigId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    
    -- Booking window: min/max advance days
    MinAdvanceDays INT DEFAULT 0, -- Can book same day if >= 0
    MaxAdvanceDays INT DEFAULT 7, -- Can book up to 7 days ahead
    
    -- Business hours
    BookingWindowStartTime TIME,
    BookingWindowEndTime TIME,
    
    -- Default locations (JSON array of LocationIds)
    AllowedLocationIds TEXT, -- JSON: ["uuid-1", "uuid-2"]
    
    -- Notification preferences
    SendFreezeReminder BOOLEAN DEFAULT TRUE,
    ReminderMinutesBefore INT DEFAULT 60,
    
    UpdatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    UpdatedBy UUID NOT NULL REFERENCES "User"(UserId),
    
    CONSTRAINT bookingconfig_tenant_unique UNIQUE(TenantId),
    CONSTRAINT bookingconfig_min_max_check CHECK(MinAdvanceDays <= MaxAdvanceDays)
);
```

---

## Security & Compliance

### OtpAttempt
Rate limiting and security audit.

```sql
CREATE TABLE OtpAttempt (
    OtpAttemptId UUID PRIMARY KEY,
    Mobile VARCHAR(20) NOT NULL,
    OtpCode VARCHAR(6) NOT NULL,
    AttemptCount INT DEFAULT 1,
    MaxAttempts INT DEFAULT 3,
    ExpiresAt TIMESTAMP WITH TIME ZONE NOT NULL,
    VerifiedAt TIMESTAMP WITH TIME ZONE,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT otp_attempt_not_expired CHECK(CreatedAt > NOW() - INTERVAL '10 minutes')
);

CREATE INDEX idx_otp_mobile_expires ON OtpAttempt(Mobile, ExpiresAt);
CREATE INDEX idx_otp_created ON OtpAttempt(CreatedAt);
```

---

### TenantDomain
Domain-based approval (e.g., only @company.com can register).

```sql
CREATE TABLE TenantDomain (
    DomainId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    Domain VARCHAR(255) NOT NULL, -- "company.com"
    IsVerified BOOLEAN DEFAULT FALSE,
    VerifiedAt TIMESTAMP WITH TIME ZONE,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT tenantdomain_unique UNIQUE(TenantId, Domain)
);

CREATE INDEX idx_tenantdomain_domain ON TenantDomain(Domain);
```

---

### InviteToken
Invite-based user registration.

```sql
CREATE TABLE InviteToken (
    TokenId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    Token VARCHAR(255) NOT NULL,
    Email VARCHAR(255) NOT NULL,
    InvitedBy UUID NOT NULL REFERENCES "User"(UserId),
    ExpiresAt TIMESTAMP WITH TIME ZONE NOT NULL,
    UsedAt TIMESTAMP WITH TIME ZONE,
    UsedBy UUID REFERENCES "User"(UserId),
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT invitetoken_token_unique UNIQUE(Token),
    CONSTRAINT invitetoken_email_tenant_unique UNIQUE(TenantId, Email)
);

CREATE INDEX idx_invitetoken_tenant_unused ON InviteToken(TenantId, UsedAt)
    WHERE UsedAt IS NULL;
```

---

## Service Calendar & Availability

### ServiceCalendar
Mark holidays, maintenance windows when bookings are not allowed.

```sql
CREATE TABLE ServiceCalendar (
    CalendarId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    CalendarDate DATE NOT NULL,
    Type VARCHAR(50) NOT NULL, -- Open, Holiday, Maintenance, Emergency
    Description VARCHAR(255),
    
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT servicecalendar_unique UNIQUE(TenantId, CalendarDate),
    CONSTRAINT servicecalendar_type_check CHECK(
        Type IN ('Open', 'Holiday', 'Maintenance', 'Emergency')
    )
);

CREATE INDEX idx_servicecalendar_tenant_date ON ServiceCalendar(TenantId, CalendarDate);
CREATE INDEX idx_servicecalendar_type ON ServiceCalendar(Type);
```

---

## Notifications & Export

### Notification
Track all user notifications.

```sql
CREATE TABLE Notification (
    NotificationId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    UserId UUID NOT NULL REFERENCES "User"(UserId),
    Type VARCHAR(100) NOT NULL, -- BookingCreated, FreezeReminder, FreezeHappened
    Title VARCHAR(255) NOT NULL,
    Message TEXT NOT NULL,
    DeliveryChannel VARCHAR(50) DEFAULT 'SMS', -- SMS, Email, InApp
    IsRead BOOLEAN DEFAULT FALSE,
    ReadAt TIMESTAMP WITH TIME ZONE,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT notification_type_check CHECK(
        Type IN ('BookingCreated', 'BookingConfirmed', 'FreezeReminder', 
                 'FreezeHappened', 'RosterExported', 'BookingRejected')
    )
);

CREATE INDEX idx_notification_user_read ON Notification(UserId, IsRead);
CREATE INDEX idx_notification_created ON Notification(CreatedAt);
```

---

### ExportTemplate
Pluggable export formats.

```sql
CREATE TABLE ExportTemplate (
    TemplateId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    Name VARCHAR(100) NOT NULL, -- "BasicCSV", "GroupedByLocation"
    Description VARCHAR(255),
    Format VARCHAR(50) DEFAULT 'CSV', -- CSV, Excel, JSON
    
    -- Column configuration (JSON)
    Columns TEXT NOT NULL, -- JSON: [{"name": "UserName", "header": "Name"}, ...]
    
    -- Optional grouping
    GroupBy VARCHAR(50), -- null, "Location", "ShiftSlot"
    SortBy VARCHAR(50), -- "UserName", "Location"
    
    IsDefault BOOLEAN DEFAULT FALSE,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    
    CONSTRAINT exporttemplate_name_tenant_unique UNIQUE(TenantId, Name)
);

CREATE INDEX idx_exporttemplate_tenant_default ON ExportTemplate(TenantId, IsDefault);
```

---

## Indexes & Performance

```sql
-- Optimize common queries
CREATE INDEX idx_booking_frozen_status ON Booking(TenantId, BookingDate, Status) 
    WHERE Status = 'Frozen';

CREATE INDEX idx_otp_active ON OtpAttempt(Mobile) 
    WHERE ExpiresAt > NOW() AND VerifiedAt IS NULL;

CREATE INDEX idx_user_active_approved ON "User"(TenantId, IsActive, IsApproved);

-- Partial indexes for faster queries
CREATE INDEX idx_notification_unread ON Notification(UserId, CreatedAt)
    WHERE IsRead = FALSE;

CREATE INDEX idx_rostersnapshot_latest ON RosterSnapshot(RosterId, CreatedAt)
    WHERE RosterId IS NOT NULL;
```

---

## Summary

| Feature | Table(s) | Purpose |
|---------|----------|---------|
| Configurable slots | ShiftSlot | Replaces hardcoded "11 PM only" |
| Per-slot freeze | ShiftSlot + ShiftFreezeOverride | Freeze time per shift, per-date overrides |
| Location control | Location | Predefined locations, not free text |
| Audit trail | BookingAudit | Full change history |
| State machine | Roster + booking_status | Open → Frozen → Exported progression |
| Immutable snapshots | RosterSnapshot | No export inconsistencies |
| Timezone support | Tenant.TimeZone | Correct freeze times globally |
| Approval methods | TenantDomain + InviteToken | Domain-based or invite-based registration |
| OTP security | OtpAttempt | Rate limiting, expiry, retry limits |
| Service availability | ServiceCalendar | Holiday/maintenance handling |
| Export templates | ExportTemplate | Pluggable formats |
| Notifications | Notification | All event notifications |
