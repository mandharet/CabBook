# Contributing to CabBook

Thank you for wanting to contribute to CabBook! This document provides guidelines and instructions for developing and contributing to the project.

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Making Changes](#making-changes)
- [Code Style Guidelines](#code-style-guidelines)
- [Testing](#testing)
- [Submitting Changes](#submitting-changes)
- [Pull Request Process](#pull-request-process)

---

## Code of Conduct

Please be respectful and constructive in all interactions:
- ✅ Ask questions if something is unclear
- ✅ Help other contributors
- ✅ Provide constructive feedback
- ❌ No discrimination or harassment
- ❌ No spam or promotional content

---

## Getting Started

### Prerequisites

- **Windows 11** or **macOS** or **Linux**
- **.NET 10 SDK** ([download](https://dotnet.microsoft.com/download))
- **Docker & Docker Compose** ([download](https://www.docker.com/products/docker-desktop))
- **Git** ([download](https://git-scm.com/))
- **VS Code** or **Visual Studio** (optional)

### Clone the Repository

```bash
git clone https://github.com/yourusername/CabBook.git
cd CabBook
```

### Verify Prerequisites

```bash
# Check .NET 10
dotnet --version

# Check Docker
docker --version
docker-compose --version

# Check Git
git --version
```

---

## Development Setup

### 1. Copy Environment File

```bash
cp .env.example .env
# Edit .env and set your local values
```

### 2. Start Backend & Database

```bash
docker-compose up --build

# Wait for PostgreSQL to be ready (check logs)
# Backend will start on http://localhost:8080
```

### 3. Verify Setup

```bash
# In another terminal
curl http://localhost:8080/api/auth/send-otp \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","tenantId":1}'

# Should return: {"message":"OTP sent to email"}
```

### 4. Run Tests

```bash
cd Backend
dotnet test

# Or with coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

---

## Making Changes

### 1. Create a Feature Branch

```bash
# Pull latest changes
git fetch origin
git rebase origin/main

# Create feature branch
git checkout -b feature/your-feature-name

# Or for bug fixes
git checkout -b fix/bug-description
```

### Branch Naming Convention

- `feature/feature-name` - New feature
- `fix/bug-name` - Bug fix
- `docs/doc-update` - Documentation
- `refactor/component-name` - Refactoring
- `test/test-name` - Tests
- `perf/optimization-name` - Performance improvement

### 2. Make Your Changes

Follow the code style guidelines below.

### 3. Commit Changes

```bash
# Stage changes
git add .

# Commit with descriptive message
git commit -m "feat: add new booking validation"
git commit -m "fix: prevent duplicate booking"
git commit -m "docs: update PWA guide"

# Push to your fork
git push origin feature/your-feature-name
```

### Commit Message Format

```
<type>: <subject>

<body>

<footer>
```

**Types:**
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation
- `style:` - Code style (formatting, semicolons, etc.)
- `refactor:` - Code refactoring
- `perf:` - Performance improvement
- `test:` - Tests
- `chore:` - Build, dependencies, tooling
- `ci:` - CI/CD configuration

**Example:**
```
feat: add OTP rate limiting with exponential backoff

- Limit to 5 requests per hour
- Increase delay after failed attempts
- Store attempts in otp_attempts table
- Add unit tests for rate limiter

Closes #123
```

---

## Code Style Guidelines

### C# (.NET)

**Naming Conventions:**
```csharp
// Classes, methods, properties → PascalCase
public class BookingService
{
    public async Task<Booking> CreateBookingAsync(long tenantId, ...)
    {
        // Local variables → camelCase
        var bookingDate = DateTime.UtcNow;
        
        // Private fields → _camelCase
        private readonly NpgsqlDataSource _dataSource;
    }
}

// Interfaces → IPascalCase
public interface IBookingService { }

// Constants → CONSTANT_CASE
private const string BOOKING_STATUS_CONFIRMED = "Confirmed";
```

**Code Style:**
```csharp
// Use var when type is obvious
var bookings = await _repository.GetBookings();

// Use null-coalescing
string name = user?.Name ?? "Guest";

// Use async/await
public async Task<Booking> GetBookingAsync(long id)

// Avoid nested ternaries
var status = IsConfirmed ? "Confirmed" : IsPending ? "Pending" : "Unknown";

// Use pattern matching
if (booking is { Status: "Confirmed", BookingDate: > tomorrow })
{
    // ...
}
```

**Line Length:**
- Maximum 120 characters
- Break long lines logically

### JavaScript/TypeScript

**Naming Conventions:**
```javascript
// Functions → camelCase
function createBooking(data) { }

// Classes/Constructors → PascalCase
class BookingService { }

// Constants → CONSTANT_CASE
const MAX_RETRIES = 3;

// Private members → _camelCase
const _cache = new Map();
```

**Code Style:**
```javascript
// Use const by default, let if needed
const { user } = this.state;
let count = 0;

// Use template literals
const message = `Booking created: ${booking.id}`;

// Use arrow functions
const bookings = items.map(item => item.booking);

// Use destructuring
const { email, phone } = user;

// Async/await over .then()
const result = await API.bookings.create(data);
```

### SQL

**Naming Conventions:**
```sql
-- Tables → snake_case (plural)
CREATE TABLE bookings (
    -- Columns → snake_case
    id BIGSERIAL PRIMARY KEY,
    booking_date DATE NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Indexes → idx_table_column
CREATE INDEX idx_bookings_user_id ON bookings(user_id);

-- Foreign keys → fk_table_foreign_key
ALTER TABLE bookings ADD CONSTRAINT fk_bookings_user_id
FOREIGN KEY (user_id) REFERENCES users(id);
```

**Format:**
```sql
-- Use uppercase for keywords
SELECT id, email, created_at
FROM users
WHERE tenant_id = @TenantId
ORDER BY created_at DESC;

-- Multi-line for complex queries
SELECT b.*, u.email, s.name as shift_name
FROM bookings b
JOIN users u ON b.user_id = u.id
JOIN shift_slots s ON b.shift_slot_id = s.id
WHERE b.tenant_id = @TenantId
  AND b.booking_date >= @FromDate
  AND b.booking_date <= @ToDate
ORDER BY b.booking_date DESC;
```

---

## Testing

### Unit Tests

```csharp
[Fact]
public async Task CreateBooking_WithValidData_ReturnsBooking()
{
    // Arrange
    var service = new BookingService(_dataSource);
    var tenantId = 1L;
    var userId = 1L;
    var shiftSlotId = 1L;
    var locationId = 1L;
    var bookingDate = DateTime.Today.AddDays(1);

    // Act
    var result = await service.CreateBookingAsync(
        tenantId, userId, shiftSlotId, locationId, bookingDate);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("Confirmed", result.Status);
}

[Theory]
[InlineData(-1)]
[InlineData(0)]
public async Task CreateBooking_WithInvalidTenantId_ThrowsException(long tenantId)
{
    var service = new BookingService(_dataSource);
    
    await Assert.ThrowsAsync<ArgumentException>(
        () => service.CreateBookingAsync(tenantId, 1, 1, 1, DateTime.Today));
}
```

### Integration Tests

```csharp
[Collection("Database collection")]
public class BookingIntegrationTests
{
    private readonly DatabaseFixture _fixture;

    public BookingIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAndRetrieveBooking()
    {
        using var connection = _fixture.GetConnection();
        var service = new BookingService(connection);

        var booking = await service.CreateBookingAsync(...);
        var retrieved = await service.GetBookingAsync(booking.Id);

        Assert.Equal(booking.Id, retrieved.Id);
    }
}
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~BookingServiceTests"

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run and watch for changes
dotnet watch test
```

---

## Submitting Changes

### 1. Push to Your Fork

```bash
git push origin feature/your-feature-name
```

### 2. Create a Pull Request

Go to GitHub and create a PR with:

**Title:** Clear and concise
```
Add OTP rate limiting to prevent brute force attacks
```

**Description:**
```markdown
## What Changed
- Added rate limiting (5 requests/hour) to OTP endpoint
- Implemented exponential backoff for failed attempts
- Added `otp_attempts` table tracking

## Why
Prevent brute force attacks on OTP verification

## Testing
- ✅ Unit tests for rate limiter
- ✅ Integration test with PostgreSQL
- ✅ Manual testing on mobile
- ✅ Offline mode still works

## Checklist
- [x] Code follows style guidelines
- [x] Tests added/updated
- [x] Documentation updated
- [x] No breaking changes
- [x] Tested locally
```

### 3. Address Review Comments

- Make requested changes
- Push new commits
- Re-request review when done

### 4. Merge

Once approved, your PR will be merged to `main`.

---

## Pull Request Process

### Before Submitting

- [ ] Code follows style guidelines (run `.editorconfig`)
- [ ] All tests pass (`dotnet test`)
- [ ] No console errors/warnings
- [ ] Documentation updated if needed
- [ ] Commit messages are clear
- [ ] No unrelated changes included
- [ ] Tested locally with Docker
- [ ] Mobile/responsive tested (if UI changes)

### PR Title Format

```
<type>(<scope>): <subject>

example:
feat(booking): add OTP rate limiting
fix(auth): prevent token replay attacks
docs(mobile): update PWA installation guide
```

### What Happens Next

1. **Automated Checks**
   - Linting
   - Build verification
   - Test coverage

2. **Code Review**
   - One or more reviewers
   - Feedback on code quality
   - Security review

3. **Approval & Merge**
   - At least one approval needed
   - Auto-merge on main branch
   - Closes associated issues

---

## Development Workflow Example

```bash
# 1. Create feature branch
git checkout -b feature/add-export-templates

# 2. Make changes
# - Edit Backend/Services/ExportService.cs
# - Add new table to schema.sql
# - Add controller endpoint
# - Write tests

# 3. Commit changes
git add .
git commit -m "feat: add pluggable export templates

- Create ExportTemplate table
- Add ExportTemplateService
- Add API endpoint /api/export-template
- Support CSV, Excel, JSON formats
- Add integration tests

Closes #456"

# 4. Push to fork
git push origin feature/add-export-templates

# 5. Create PR on GitHub
# - Fill in PR template
# - Wait for review
# - Address feedback

# 6. Merge and cleanup
git branch -d feature/add-export-templates
```

---

## Questions?

- **Documentation:** Check [docs/](./md/)
- **Issues:** Search [GitHub Issues](https://github.com/yourusername/CabBook/issues)
- **Discussions:** Ask in GitHub Discussions
- **Email:** contact@cabbook.com

---

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.

---

## Recognition

Contributors are recognized in:
- Git commit history
- GitHub contributors page
- CONTRIBUTORS.md file

Thank you for contributing to CabBook! 🎉
