# Quick Start Checklist: Build CabBook in 2 Hours

Follow this checklist to have a live, working cab booking system by the end of today.

---

## ⏱️ Timeline: 2 Hours Total

```
Setup:              30 min
Backend:            45 min
Frontend:           30 min
Testing:            10 min
Deploy:             5 min
━━━━━━━━━━━━━━━━━━━━
Total:              2 hours (2:00 - 4:00 PM)
```

---

## 📋 PART 1: Setup (30 minutes)

### Pre-flight Check
- [ ] .NET 8 SDK installed: `dotnet --version`
- [ ] PostgreSQL available (Docker or local)
- [ ] Git configured: `git config user.name`
- [ ] Editor ready (VS Code, Visual Studio, etc.)

### Create Project
```bash
# [ ] Create directories
mkdir CabBook
cd CabBook

# [ ] Create backend
dotnet new webapi -n Backend -f net8.0

# [ ] Remove boilerplate
cd Backend
rm WeatherForecast.cs Controllers/WeatherForecastController.cs

# [ ] Add .gitignore
echo "bin/
obj/
appsettings.local.json
.env
*.db" > .gitignore

cd ..
```

### Add Dependencies
```bash
cd Backend

# [ ] Add 4 packages (total)
dotnet add package Npgsql
dotnet add package System.IdentityModel.Tokens.Jwt

cd ..
```

### Database Setup
```bash
# [ ] Start PostgreSQL
docker run --name cabbook-db \
  -e POSTGRES_PASSWORD=cabbook \
  -e POSTGRES_DB=cabbook \
  -p 5432:5432 \
  -d postgres:16-alpine

# [ ] Wait 5 seconds for startup
sleep 5

# [ ] Create schema.sql (see 06-GETTING_STARTED.md for content)
# [ ] Load schema
psql -h localhost -U postgres -d cabbook -f database/schema.sql
```

---

## 💻 PART 2: Backend Code (45 minutes)

### Core Models (5 min)
- [ ] Create `Models/User.cs`
- [ ] Create `Models/Booking.cs`
- [ ] Create `Models/ShiftSlot.cs`
- [ ] Create `Models/Location.cs`
- [ ] Create `Models/OtpAttempt.cs`

(Copy from **06-GETTING_STARTED.md**)

### Services (20 min)
- [ ] Create `Services/AuthService.cs`
  - [ ] `SendOtpAsync()`
  - [ ] `VerifyOtpAsync()`
  - [ ] `GenerateToken()`

(Copy from **06-GETTING_STARTED.md**)

### Controllers (15 min)
- [ ] Create `Controllers/AuthController.cs`
  - [ ] `SendOtp()` endpoint
  - [ ] `VerifyOtp()` endpoint

(Copy from **06-GETTING_STARTED.md**)

### Configuration (5 min)
- [ ] Update `Program.cs`
  - [ ] Add CORS
  - [ ] Add database connection
  - [ ] Add JWT authentication
  - [ ] Add services
  - [ ] Add static files
- [ ] Update `appsettings.json`
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Host=localhost;Username=postgres;Password=cabbook;Database=cabbook"
    },
    "JwtSecret": "your-32-character-secret-key-here-1234567890ab"
  }
  ```

---

## 🎨 PART 3: Frontend (30 minutes)

### Create Folder Structure
```bash
# [ ] Create wwwroot directory
mkdir -p Backend/wwwroot/{css,js,images}
```

### HTML (5 min)
- [ ] Create `Backend/wwwroot/index.html`
  - [ ] Add Oat UI CSS from CDN
  - [ ] Add app div
  - [ ] Add script tags for JS files

(Copy from **06-GETTING_STARTED.md**)

### CSS (5 min)
- [ ] Create `Backend/wwwroot/css/app.css`
  - [ ] Basic layout styles
  - [ ] Card styles
  - [ ] Form styles
  - [ ] Button styles

(Copy from **06-GETTING_STARTED.md**)

### JavaScript (20 min)
- [ ] Create `Backend/wwwroot/js/api.js`
  - [ ] `API.call()` function with auth
  
- [ ] Create `Backend/wwwroot/js/app.js`
  - [ ] `state` object
  - [ ] `init()` function
  - [ ] `showLoginPage()`
  - [ ] `showBookingPage()`
  - [ ] Event handlers

(Copy from **06-GETTING_STARTED.md**)

---

## 🧪 PART 4: Test Locally (10 minutes)

### Run Backend
```bash
# [ ] Navigate to Backend folder
cd Backend

# [ ] Set JWT secret (optional)
dotnet user-secrets set "JwtSecret" "your-32-char-secret-key-12345"

# [ ] Run server
dotnet run

# [ ] Verify running: http://localhost:5000
# [ ] Should see your index.html
```

### Test Flow
- [ ] Open http://localhost:5000 in browser
- [ ] Enter mobile number: `9876543210`
- [ ] Click "Send OTP"
- [ ] Check console output for OTP (should print)
- [ ] Enter OTP in browser
- [ ] Click "Verify"
- [ ] Should see booking page
- [ ] Select date and location
- [ ] Click "Book"
- [ ] See success notification

---

## 🚀 PART 5: Deploy to Render (5 minutes)

### Create Docker Setup
```bash
# [ ] Create `Dockerfile` in root
# [ ] Create `docker-compose.yml` in root

(Copy from **05-LIGHTWEIGHT_ARCHITECTURE.md**)
```

### Push to GitHub
```bash
# [ ] In project root
git init
git add .
git commit -m "Initial cab booking system - lightweight stack"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/cabbook.git
git push -u origin main
```

### Deploy on Render
- [ ] Go to https://render.com
- [ ] Click "New +" → "Web Service"
- [ ] Connect GitHub (authorize if needed)
- [ ] Select your `cabbook` repository
- [ ] Configure:
  - [ ] Name: `cabbook`
  - [ ] Runtime: `Docker`
  - [ ] Region: Any
  - [ ] Branch: `main`
  - [ ] Dockerfile: `./Dockerfile`
- [ ] Add Environment Variable:
  - [ ] Name: `ConnectionStrings__DefaultConnection`
  - [ ] Value: `postgresql://postgres:cabbook@localhost:5432/cabbook`
  - [ ] ⚠️ **Create PostgreSQL on Render first**:
    - [ ] Click "New +" → "PostgreSQL"
    - [ ] Name: `cabbook-db`
    - [ ] Copy connection string when created
    - [ ] Paste into ConnectionString env var
- [ ] Click "Create Web Service"
- [ ] Wait for deploy (~3 minutes)
- [ ] Visit your URL: `https://cabbook-xxxxx.onrender.com`
- [ ] Test full flow again

---

## ✅ Final Checklist

### Code Quality
- [ ] No compilation errors: `dotnet build`
- [ ] No unused imports
- [ ] Models have proper types
- [ ] API responses are consistent

### Testing
- [ ] Login flow works (OTP)
- [ ] Create booking works
- [ ] Database saves booking
- [ ] Frontend updates on success
- [ ] Error messages display

### Deployment
- [ ] Dockerfile builds without errors
- [ ] Docker image runs locally: `docker run ...`
- [ ] Render deployment successful
- [ ] Live URL is accessible
- [ ] Live URL booking works end-to-end

### Documentation
- [ ] README.md updated with live URL
- [ ] API endpoints documented
- [ ] Database schema documented
- [ ] Setup instructions clear

---

## 📊 What You Have Now

```
✅ Working cab booking system
✅ User authentication (OTP)
✅ Booking creation & management
✅ Database (PostgreSQL)
✅ Live deployment on Render
✅ Responsive UI (Oat CSS)
✅ Zero dependencies headache
✅ <$25/month cost
```

---

## 🎯 Next Steps (After MVP Works)

### Add Features (Each 30-45 min)
1. [ ] Add booking validation
2. [ ] Add freeze automation
3. [ ] Add booking audit logging
4. [ ] Add admin export
5. [ ] Add shift slots UI

### Add Polish (15-30 min each)
6. [ ] Add loading indicators
7. [ ] Add error handling
8. [ ] Add mobile responsiveness
9. [ ] Add form validation
10. [ ] Add notification system

### Deploy Improvements
11. [ ] Set up CI/CD (GitHub Actions)
12. [ ] Add database backups
13. [ ] Add monitoring
14. [ ] Add custom domain
15. [ ] Add SSL certificate (auto on Render)

---

## ⚠️ Troubleshooting

### "Connection refused"
```bash
# Check PostgreSQL is running
docker ps | grep cabbook-db

# If not running:
docker start cabbook-db
```

### "Unable to resolve service"
- Check `Program.cs` has all service registrations
- Restart `dotnet run`

### "401 Unauthorized"
- Check JWT token in localStorage
- Check `JwtSecret` matches in code

### "CORS error in console"
- Check CORS is enabled in `Program.cs`
- Frontend URL should match API origin

### "CSS not loading"
- Check Oat CSS CDN link is correct
- Check `app.css` is in `wwwroot/css/`

### "Database migrations failed"
- Ensure PostgreSQL is running
- Run schema SQL manually: `psql ... -f schema.sql`
- Check database user permissions

---

## 💰 Cost Verification

### Render Free Tier Includes
- ✅ 750 compute hours/month (enough for 24/7)
- ✅ 100 GB bandwidth
- ✅ PostgreSQL 256MB storage
- ✅ Auto HTTPS

### When You'll Need to Pay
- Monthly usage > 750 hours (24/7 = ~730 hours, so just over)
- Database > 256MB
- Bandwidth > 100GB

**Action:** Before month-end, upgrade to Pro ($7/month) if still free tier.

---

## 📞 Questions?

If anything doesn't work:

1. Check **06-GETTING_STARTED.md** for detailed code
2. Check **05-LIGHTWEIGHT_ARCHITECTURE.md** for architecture explanation
3. Check error logs: `dotnet run` shows all errors
4. Check browser console: F12 → Console tab for JS errors

---

## 🎉 Success Criteria

You've succeeded when:

- [ ] http://localhost:5000 loads your app
- [ ] Can login with OTP
- [ ] Can create booking
- [ ] Booking shows in "My Bookings"
- [ ] Live URL on Render works
- [ ] Live URL is fast (<1s load)
- [ ] Total monthly cost will be <$25

---

## Time Tracking

```
Task             Planned  Actual  Notes
────────────────────────────────────────
Setup            30 min   _____   
Backend          45 min   _____
Frontend         30 min   _____
Testing          10 min   _____
Deployment       5 min    _____
────────────────────────────────────────
TOTAL            2 hours  _____
```

---

## 🚀 You're Ready!

Start with **Part 1** now. You should be live by end of this checklist. 

Good luck! 💪

---

**Remember:** The goal is done > perfect. Ship the MVP, then improve.
