What issues are in current design ?

Your current design is actually good for an MVP, but there are several hidden problems that will appear once real users start using it daily.

I’ll break this down like a real architecture review.

🚨 1. Biggest Issue — “Tomorrow Only” Logic

Right now:

Users can only book for next day

Sounds simple, but creates problems:

Problems
Night shift changes
Weekend/holiday bookings
Long weekends
Public holidays
Users forgetting before freeze

Example:

Friday freeze passed
User needs Monday cab
System blocks booking
Better Design

Instead of:

Only tomorrow

Use:

Booking window = configurable

Example:

Min advance = 0
Max advance = 7 days

This gives future flexibility.

🚨 2. Freeze Time Ambiguity

You said:

Freeze time configurable

But configurable for WHAT?

Currently unclear:

Per tenant?
Per slot?
Per day?
Global?
Real Problem

Later you add:

7 PM drop
11 PM drop

Then:

Different freeze times needed

Example:

7 PM slot → freeze at 4 PM
11 PM slot → freeze at 8 PM
Better Design

Freeze should belong to:

Shift/Slot

NOT global config.

🚨 3. “11 PM Only” Hardcoding Trap

You keep saying:

only 11 PM for now

Danger:

Teams hardcode everywhere
Later expansion becomes painful
Bad
if slot == "11PM"
Better

Create:

ShiftSlot table

Even if it has only 1 record today.

🚨 4. Missing Location Strategy (Critical)

Currently:

You mentioned export roster
But transport team needs grouping

Without structured location:

Data becomes unusable
Current Risk

Users enter:

Powai
powai
POWAI
Near Powai lake

Transport team suffers.

Better

Use:

Option A

Predefined locations

OR

Option B

Office transport zones

Example:

Powai
Andheri
Thane
Navi Mumbai
🚨 5. No Cancellation Policy

What happens if:

User books
Freeze not reached
User cancels?
Rebooks?
Changes location?

Currently undefined.

Better Rules

Before freeze:

Edit allowed
Cancel allowed

After freeze:

Locked
🚨 6. No Audit Trail

This becomes BIG in enterprise environments.

Example:

"I booked!"
"No you didn't"

You need logs.

Add

BookingAudit table:

Created
Updated
Cancelled
Timestamp
User
🚨 7. Tenant Isolation Weakness

You said:

shared DB + TenantId

Good start.

But people forget:

EVERY query must filter tenant
Risk

Data leak between companies.

Better

Use:

Tenant middleware
Scoped repositories
🚨 8. OTP Abuse Problem

Current design vulnerable to:

OTP spam
SMS cost abuse
Add
Rate limiting
OTP expiry
Max retries
🚨 9. Freeze Job Timing Problems

What if:

Server time mismatch?
Job fails?
Restart happens during freeze?
Better

Make freeze:

State-based

Instead of:

current time > freeze

Use:

RosterStatus
- Open
- Frozen
- Exported
🚨 10. Export Snapshot Problem

Huge hidden issue.

If admin exports:

8:01 PM

Then another booking sneaks in:

8:02 PM

Roster mismatch.

Better

At freeze:

Create immutable snapshot

Never generate dynamically afterward.

🚨 11. No Timezone Handling

Very common bug.

Especially if:

Server UTC
Users IST

Freeze breaks.

Better

Store:

UTC in DB
Convert at UI
🚨 12. Approval Flow Incomplete

Currently:

User registers → admin approves

But:

How does admin know tenant?
Can anyone register?
Invite-only?
Better

Add:

Domain/company mapping
OR
Invite codes
OR
Employee ID verification
🚨 13. Export Format Will Break Later

Today:

CSV

Later transport asks:

Separate sheets
Grouping
Different vendor formats
Better

Use:

ExportTemplate

per tenant eventually.

🚨 14. No Notification Strategy

Users need:

Booking success
Freeze reminder
Booking rejected

Without this:
Support burden increases.

🚨 15. Missing Availability Controls

What if:

Transport disabled today?
Holiday?
No cab service?

Need:

ServiceCalendar
🧠 Biggest Architectural Advice

Right now your system is:

Feature-oriented

You should shift to:

Configuration-oriented

Meaning:

Slots configurable
Freeze configurable
Booking rules configurable
✅ What You DID RIGHT

You correctly avoided:

Real-time cab tracking
Driver management
Route optimization

That saved you from massive complexity.

🔥 Most Important Improvements To Do NOW

If you do ONLY 5 things:

1.

Create ShiftSlot table

2.

Structured locations

3.

Immutable roster snapshot

4.

Booking audit logs

5.

Configurable booking window

🚀 Final Assessment

Your current design is:

✅ Strong MVP
❌ Not yet enterprise-safe
❌ Not yet scale-safe

But the foundation is good.

Most dangerous risk:
👉 Hardcoding “11 PM tomorrow only”

That technical debt will explode later.