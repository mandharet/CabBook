// CabBook SPA - Minimal & Professional
const App = {
    state: {
        user: null,
        currentPage: 'auth',
        config: null,
        bookings: [],
        shiftSlots: [],
        locations: [],
        rosters: [],
        pendingUsers: [],
        tenantId: 1
    },

    async init() {
        this.restoreAuth();
        this.setupRouter();
        window.addEventListener('hashchange', () => this.router());
        this.router();
    },

    restoreAuth() {
        const token = localStorage.getItem('token');
        if (token) {
            API.setToken(token);
            this.state.user = this.parseJwt(token);
        }
    },

    parseJwt(token) {
        try {
            const base64Url = token.split('.')[1];
            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
            const jsonPayload = decodeURIComponent(atob(base64).split('').map(c =>
                '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)
            ).join(''));
            return JSON.parse(jsonPayload);
        } catch (e) {
            return null;
        }
    },

    setupRouter() {
        const routes = {
            '/': 'home',
            '/bookings': 'bookings',
            '/admin': 'admin',
            '/logout': 'logout'
        };
        window.routes = routes;
    },

    async router() {
        const hash = window.location.hash.slice(1) || '/';
        const route = window.routes[hash] || 'home';

        if (!this.state.user && route !== 'auth' && hash !== '/') {
            window.location.hash = '#/';
            return;
        }

        if (route === 'logout') {
            API.logout();
            this.state.user = null;
            window.location.hash = '#/';
            return;
        }

        this.state.currentPage = route;
        this.updateUI();
    },

    async updateUI() {
        const { currentPage } = this.state;
        const navbar = document.getElementById('navbar');
        const bottomNav = document.getElementById('bottomNav');

        // Show/hide navigation based on auth state
        if (!this.state.user) {
            navbar.style.display = 'none';
            bottomNav.style.display = 'none';
        } else {
            navbar.style.display = 'block';
            bottomNav.style.display = 'flex';
        }

        // Update active nav item
        document.querySelectorAll('.nav-item').forEach(item => {
            item.classList.remove('active');
        });
        const activeNav = document.querySelector(`[data-page="${currentPage}"]`);
        if (activeNav) {
            activeNav.classList.add('active');
        }

        // Show/hide admin nav
        const adminNav = document.getElementById('adminNav');
        if (adminNav) {
            adminNav.style.display = this.state.user?.role === 'Admin' ? 'block' : 'none';
        }

        const main = document.getElementById('main');

        try {
            if (!this.state.user) {
                main.innerHTML = document.getElementById('auth-page').innerHTML;
                this.setupAuthPage();
            } else if (currentPage === 'home') {
                main.innerHTML = document.getElementById('home-page').innerHTML;
                this.setupHomePage();
            } else if (currentPage === 'bookings') {
                main.innerHTML = document.getElementById('bookings-page').innerHTML;
                await this.setupBookingsPage();
            } else if (currentPage === 'admin') {
                main.innerHTML = document.getElementById('admin-page').innerHTML;
                await this.setupAdminPage();
            }
        } catch (error) {
            console.error('Error rendering page:', error);
            main.innerHTML = `<div style="padding: 2rem; text-align: center; color: #d32f2f;">Error: ${error.message}</div>`;
        }
    },

    setupAuthPage() {
        const loginTab = document.getElementById('login-tab');
        const signupTab = document.getElementById('signup-tab');
        const loginForm = document.getElementById('loginForm');
        const signupForm = document.getElementById('signupForm');
        const switchToSignup = document.getElementById('switchToSignup');
        const switchToLogin = document.getElementById('switchToLogin');
        const otpPrompt = document.getElementById('otpPrompt');
        const verifyBtn = document.getElementById('verifyBtn');

        // Tab switching
        switchToSignup.onclick = (e) => {
            e.preventDefault();
            loginTab.classList.remove('active');
            signupTab.classList.add('active');
        };

        switchToLogin.onclick = (e) => {
            e.preventDefault();
            signupTab.classList.remove('active');
            loginTab.classList.add('active');
        };

        // Login form
        loginForm.onsubmit = async (e) => {
            e.preventDefault();
            const email = document.getElementById('loginEmail').value;

            try {
                await API.auth.sendOtp(email, this.state.tenantId);
                otpPrompt.style.display = 'block';
                loginForm.querySelector('button[type="submit"]').disabled = true;

                verifyBtn.onclick = async () => {
                    const otp = document.getElementById('otp').value;
                    try {
                        const token = await API.auth.verifyOtp(email, otp, this.state.tenantId);
                        API.setToken(token);
                        this.state.user = this.parseJwt(token);
                        await this.loadConfig();
                        window.location.hash = '#/';
                    } catch (error) {
                        alert('Login failed: ' + error.message);
                    }
                };
            } catch (error) {
                alert('Error: ' + error.message);
            }
        };

        // Signup form
        signupForm.onsubmit = async (e) => {
            e.preventDefault();
            const email = document.getElementById('signupEmail').value;
            const name = document.getElementById('signupName').value || null;
            const phone = document.getElementById('signupPhone').value || null;
            const pickupAddress = document.getElementById('signupPickup').value;
            const dropoffAddress = document.getElementById('signupDropoff').value;

            try {
                await API.auth.signup(email, phone, name, pickupAddress, dropoffAddress, this.state.tenantId);
                alert('Signup successful! Admin will review and approve your account.');
                signupForm.reset();
                signupTab.classList.remove('active');
                loginTab.classList.add('active');
            } catch (error) {
                alert('Error: ' + error.message);
            }
        };
    },

    setupHomePage() {
        const userNameEl = document.getElementById('userName');
        if (this.state.user?.email) {
            userNameEl.textContent = `Hi, ${this.state.user.email.split('@')[0]}`;
        }
    },

    async setupBookingsPage() {
        await this.loadConfig();
        await this.loadBookings();

        const form = document.getElementById('bookingForm');
        const shiftSelect = document.getElementById('shiftSelect');
        const locationSelect = document.getElementById('locationSelect');

        // Populate dropdowns
        shiftSelect.innerHTML = '<option value="">Select a shift</option>';
        this.state.config?.ShiftSlots?.forEach(slot => {
            const option = document.createElement('option');
            option.value = slot.id;
            option.textContent = `${slot.name} (${this.formatTime(slot.start_time)} - ${this.formatTime(slot.end_time)})`;
            shiftSelect.appendChild(option);
        });

        locationSelect.innerHTML = '<option value="">Select a location</option>';
        this.state.config?.Locations?.forEach(loc => {
            const option = document.createElement('option');
            option.value = loc.id;
            option.textContent = loc.name;
            locationSelect.appendChild(option);
        });

        // Set min date
        const tomorrow = new Date();
        tomorrow.setDate(tomorrow.getDate() + 1);
        document.getElementById('bookingDate').min = tomorrow.toISOString().split('T')[0];

        form.onsubmit = async (e) => {
            e.preventDefault();

            const shiftSlotId = parseInt(shiftSelect.value);
            const locationId = parseInt(locationSelect.value);
            const bookingDate = new Date(document.getElementById('bookingDate').value);

            try {
                await API.bookings.create(shiftSlotId, locationId, bookingDate);
                alert('Booking created!');
                await this.loadBookings();
                form.reset();
                this.renderBookingsList();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        };

        this.renderBookingsList();
    },

    renderBookingsList() {
        const container = document.getElementById('bookingsContainer');
        container.innerHTML = '';

        if (!this.state.bookings.length) {
            container.innerHTML = '<div class="empty-state">No bookings yet</div>';
            return;
        }

        this.state.bookings.forEach(booking => {
            const slot = this.state.config?.ShiftSlots?.find(s => s.id === booking.shift_slot_id);
            const loc = this.state.config?.Locations?.find(l => l.id === booking.location_id);

            const card = document.createElement('div');
            card.className = 'booking-card';
            card.innerHTML = `
                <h4>${new Date(booking.booking_date).toLocaleDateString()}</h4>
                <p><strong>Shift:</strong> ${slot?.name || 'Unknown'}</p>
                <p><strong>Location:</strong> ${loc?.name || 'Unknown'}</p>
                <p><strong>Status:</strong> <span class="badge">${booking.status}</span></p>
                <button class="btn-danger btn-small" onclick="App.cancelBooking(${booking.id})">Cancel</button>
            `;
            container.appendChild(card);
        });
    },

    async cancelBooking(id) {
        if (confirm('Cancel this booking?')) {
            try {
                await API.bookings.cancel(id);
                await this.loadBookings();
                this.renderBookingsList();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        }
    },

    async setupAdminPage() {
        await this.loadConfig();
        await this.loadRosters();
        await this.loadPendingUsers();

        // Tab switching
        document.querySelectorAll('.tab-btn').forEach(btn => {
            btn.onclick = (e) => {
                document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
                document.querySelectorAll('.tab-pane').forEach(t => t.classList.remove('active'));
                e.target.classList.add('active');
                document.getElementById(e.target.dataset.tab).classList.add('active');
            };
        });

        this.renderPendingUsersList();

        // Shift form
        const shiftForm = document.getElementById('shiftForm');
        shiftForm.onsubmit = async (e) => {
            e.preventDefault();
            const name = document.getElementById('shiftName').value;
            const startTime = document.getElementById('shiftStart').value;
            const endTime = document.getElementById('shiftEnd').value;
            const freezeTime = document.getElementById('shiftFreeze').value || null;

            try {
                await API.shiftSlots.create(name, startTime, endTime, freezeTime);
                alert('Shift slot created!');
                await this.loadConfig();
                this.renderShiftSlotsList();
                shiftForm.reset();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        };

        this.renderShiftSlotsList();

        // Location form
        const locForm = document.getElementById('locationForm');
        locForm.onsubmit = async (e) => {
            e.preventDefault();
            const name = document.getElementById('locName').value;
            const address = document.getElementById('locAddress').value;

            try {
                await API.locations.create(name, address);
                alert('Location created!');
                await this.loadConfig();
                this.renderLocationsList();
                locForm.reset();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        };

        this.renderLocationsList();
        this.renderRostersList();
    },

    renderPendingUsersList() {
        const container = document.getElementById('pendingContainer');
        container.innerHTML = '';

        if (!this.state.pendingUsers?.length) {
            container.innerHTML = '<div class="empty-state">No pending approvals</div>';
            return;
        }

        this.state.pendingUsers.forEach(user => {
            const card = document.createElement('div');
            card.className = 'pending-card';
            const addressStatus = user.address_status === 'approved' ? '✓ Approved' : '⏳ Pending';

            card.innerHTML = `
                <h4>${user.name || 'N/A'}</h4>
                <div class="user-info">
                    <p><strong>Email:</strong> ${user.email}</p>
                    ${user.phone_number ? `<p><strong>Phone:</strong> ${user.phone_number}</p>` : ''}
                </div>
                <div class="address-section">
                    <p><strong>Pickup:</strong> ${user.pickup_address || 'N/A'}</p>
                    <p><strong>Dropoff:</strong> ${user.dropoff_address || 'N/A'}</p>
                    <small>${addressStatus}</small>
                </div>
                <div class="approval-buttons">
                    <button class="btn-primary btn-small" onclick="App.approveUser(${user.id})">Approve User</button>
                    <button class="btn-primary btn-small" onclick="App.approveUserAddresses(${user.id})">Approve Addresses</button>
                    <button class="btn-danger btn-small" onclick="App.rejectUser(${user.id})">Reject</button>
                </div>
            `;
            container.appendChild(card);
        });
    },

    renderShiftSlotsList() {
        const container = document.getElementById('shiftsContainer');
        container.innerHTML = '';

        if (!this.state.config?.ShiftSlots?.length) {
            container.innerHTML = '<div class="empty-state">No shift slots</div>';
            return;
        }

        this.state.config.ShiftSlots.forEach(slot => {
            const item = document.createElement('div');
            item.className = 'item';
            item.innerHTML = `
                <div class="item-info">
                    <strong>${slot.name}</strong>
                    <small>${this.formatTime(slot.start_time)} - ${this.formatTime(slot.end_time)}</small>
                </div>
                <button class="btn-danger btn-small" onclick="App.deleteShiftSlot(${slot.id})">Delete</button>
            `;
            container.appendChild(item);
        });
    },

    renderLocationsList() {
        const container = document.getElementById('locationsContainer');
        container.innerHTML = '';

        if (!this.state.config?.Locations?.length) {
            container.innerHTML = '<div class="empty-state">No locations</div>';
            return;
        }

        this.state.config.Locations.forEach(loc => {
            const item = document.createElement('div');
            item.className = 'item';
            item.innerHTML = `
                <div class="item-info">
                    <strong>${loc.name}</strong>
                    <small>${loc.address || 'No address'}</small>
                </div>
                <button class="btn-danger btn-small" onclick="App.deleteLocation(${loc.id})">Delete</button>
            `;
            container.appendChild(item);
        });
    },

    renderRostersList() {
        const container = document.getElementById('rostersContainer');
        container.innerHTML = '';

        if (!this.state.rosters?.length) {
            container.innerHTML = '<div class="empty-state">No rosters</div>';
            return;
        }

        this.state.rosters.forEach(roster => {
            const slot = this.state.config?.ShiftSlots?.find(s => s.id === roster.shift_slot_id);
            const item = document.createElement('div');
            item.className = 'item';
            item.innerHTML = `
                <div class="item-info">
                    <strong>${slot?.name || 'Unknown'}</strong>
                    <small>${new Date(roster.roster_date).toLocaleDateString()} • ${roster.status}</small>
                </div>
                <div>
                    ${roster.status === 'Open' ? `<button class="btn-danger btn-small" onclick="App.freezeRoster(${roster.id})">Freeze</button>` : ''}
                    ${roster.status !== 'Open' ? `<button class="btn-success btn-small" onclick="App.exportRoster(${roster.id})">Export</button>` : ''}
                </div>
            `;
            container.appendChild(item);
        });
    },

    async deleteShiftSlot(id) {
        if (confirm('Delete this shift?')) {
            try {
                await API.shiftSlots.delete(id);
                await this.loadConfig();
                this.renderShiftSlotsList();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        }
    },

    async deleteLocation(id) {
        if (confirm('Delete this location?')) {
            try {
                await API.locations.delete(id);
                await this.loadConfig();
                this.renderLocationsList();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        }
    },

    async approveUser(id) {
        if (confirm('Approve this user?')) {
            try {
                await API.users.approve(id);
                await this.loadPendingUsers();
                this.renderPendingUsersList();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        }
    },

    async rejectUser(id) {
        if (confirm('Reject this user?')) {
            try {
                await API.users.reject(id);
                await this.loadPendingUsers();
                this.renderPendingUsersList();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        }
    },

    async approveUserAddresses(id) {
        if (confirm('Approve these addresses?')) {
            try {
                await API.users.approveAddresses(id);
                await this.loadPendingUsers();
                this.renderPendingUsersList();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        }
    },

    async freezeRoster(id) {
        if (confirm('Freeze this roster?')) {
            try {
                await API.rosters.freeze(id);
                await this.loadRosters();
                this.renderRostersList();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        }
    },

    async exportRoster(id) {
        try {
            const response = await API.rosters.export(id);
            const blob = new Blob([response], { type: 'text/csv' });
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `roster_${id}.csv`;
            a.click();
        } catch (error) {
            alert('Error: ' + error.message);
        }
    },

    async loadConfig() {
        try {
            this.state.config = await API.tenant.getConfig();
        } catch (error) {
            console.error('Error loading config:', error);
        }
    },

    async loadBookings() {
        try {
            this.state.bookings = await API.bookings.list();
        } catch (error) {
            console.error('Error loading bookings:', error);
        }
    },

    async loadRosters() {
        try {
            const from = new Date();
            from.setDate(from.getDate() - 30);
            const to = new Date();
            to.setDate(to.getDate() + 30);
            this.state.rosters = await API.rosters.list(from, to);
        } catch (error) {
            console.error('Error loading rosters:', error);
        }
    },

    async loadPendingUsers() {
        try {
            const result = await API.users.getPending();
            this.state.pendingUsers = result.users || [];
        } catch (error) {
            console.error('Error loading pending users:', error);
        }
    },

    formatTime(timeStr) {
        if (!timeStr) return '';
        const [hours, minutes] = timeStr.split(':');
        return `${hours}:${minutes}`;
    }
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    App.init();
    registerServiceWorker();
});

// PWA Service Worker Registration
function registerServiceWorker() {
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('/service-worker.js')
            .then(registration => {
                console.log('Service Worker registered');
                registration.addEventListener('updatefound', () => {
                    const newWorker = registration.installing;
                    newWorker.addEventListener('statechange', () => {
                        if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                            showUpdateNotification();
                        }
                    });
                });
            })
            .catch(error => console.error('Service Worker registration failed:', error));
    }
}

function showUpdateNotification() {
    const message = document.createElement('div');
    message.style.cssText = `
        position: fixed;
        bottom: 80px;
        left: 1rem;
        right: 1rem;
        background: #000;
        color: #fff;
        padding: 1rem;
        border-radius: 8px;
        z-index: 100;
        display: flex;
        justify-content: space-between;
        align-items: center;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
    `;
    message.innerHTML = `
        <span>Update available</span>
        <button style="background: #fff; color: #000; border: none; padding: 0.5rem 1rem; border-radius: 4px; cursor: pointer; font-weight: 600; margin-left: 1rem;" onclick="window.location.reload()">Reload</button>
    `;
    document.body.appendChild(message);
}

window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    window.installPrompt = e;
});

window.addEventListener('appinstalled', () => {
    window.installPrompt = null;
});
