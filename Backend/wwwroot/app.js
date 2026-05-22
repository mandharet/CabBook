// CabBook SPA - Single Page Application with vanilla JS
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
        tenantId: 1 // Default, should come from config
    },

    async init() {
        this.restoreAuth();
        this.setupRouter();
        this.setupEventListeners();

        // Load initial page
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

        // Update navigation - show admin nav only if user is admin
        document.getElementById('adminNav').style.display =
            this.state.user?.role === 'Admin' ? 'inline' : 'none';

        // Show/hide logout button based on authentication
        document.querySelector('.nav-logout').style.display =
            this.state.user ? 'inline' : 'none';

        const main = document.getElementById('main');

        try {
            if (!this.state.user) {
                main.innerHTML = document.getElementById('auth-page').innerHTML;
                this.setupAuthPage();
            } else if (currentPage === 'home') {
                main.innerHTML = document.getElementById('home-page').innerHTML;
            } else if (currentPage === 'bookings') {
                main.innerHTML = document.getElementById('bookings-page').innerHTML;
                await this.setupBookingsPage();
            } else if (currentPage === 'admin') {
                main.innerHTML = document.getElementById('admin-page').innerHTML;
                await this.setupAdminPage();
            }
        } catch (error) {
            console.error('Error rendering page:', error);
            main.innerHTML = `<div class="alert alert-danger">Error: ${error.message}</div>`;
        }
    },

    setupAuthPage() {
        // Tab switching for auth pages
        document.querySelectorAll('.auth-tab-btn').forEach(btn => {
            btn.onclick = (e) => {
                document.querySelectorAll('.auth-tab-btn').forEach(b => b.classList.remove('active'));
                document.querySelectorAll('.auth-tab').forEach(t => t.classList.remove('active'));
                e.target.classList.add('active');
                document.getElementById(e.target.dataset.tab).classList.add('active');
            };
        });

        // Login form
        const form = document.getElementById('loginForm');
        const otpSection = document.getElementById('otpSection');
        const otpMessage = document.getElementById('otpMessage');
        const verifyBtn = document.getElementById('verifyBtn');

        form.onsubmit = async (e) => {
            e.preventDefault();
            const email = document.getElementById('email').value;

            try {
                await API.auth.sendOtp(email, this.state.tenantId);
                otpMessage.style.display = 'block';
                otpSection.style.display = 'block';
                form.querySelector('button[type="submit"]').disabled = true;

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
        const signupForm = document.getElementById('signupForm');
        signupForm.onsubmit = async (e) => {
            e.preventDefault();
            const email = document.getElementById('signupEmail').value;
            const name = document.getElementById('signupName').value || null;
            const phone = document.getElementById('signupPhone').value || null;
            const pickupAddress = document.getElementById('signupPickup').value;
            const dropoffAddress = document.getElementById('signupDropoff').value;

            try {
                await API.auth.signup(email, phone, name, pickupAddress, dropoffAddress, this.state.tenantId);
                alert('Signup successful! Admin will review and approve your account and addresses.');
                signupForm.reset();
                // Switch back to login tab
                document.querySelector('[data-tab="login"]').click();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        };
    },

    async setupBookingsPage() {
        await this.loadConfig();
        await this.loadBookings();

        const form = document.getElementById('createBookingForm');
        const shiftSlotSelect = document.getElementById('shiftSlot');
        const locationSelect = document.getElementById('location');

        // Populate dropdowns
        this.state.config?.ShiftSlots?.forEach(slot => {
            const option = document.createElement('option');
            option.value = slot.id;
            option.textContent = `${slot.name} (${this.formatTime(slot.start_time)} - ${this.formatTime(slot.end_time)})`;
            shiftSlotSelect.appendChild(option);
        });

        this.state.config?.Locations?.forEach(loc => {
            const option = document.createElement('option');
            option.value = loc.id;
            option.textContent = loc.name;
            locationSelect.appendChild(option);
        });

        // Set min date (tomorrow)
        const tomorrow = new Date();
        tomorrow.setDate(tomorrow.getDate() + 1);
        document.getElementById('bookingDate').min = tomorrow.toISOString().split('T')[0];

        form.onsubmit = async (e) => {
            e.preventDefault();

            const shiftSlotId = parseInt(document.getElementById('shiftSlot').value);
            const locationId = parseInt(document.getElementById('location').value);
            const bookingDate = new Date(document.getElementById('bookingDate').value);

            try {
                await API.bookings.create(shiftSlotId, locationId, bookingDate);
                alert('Booking created!');
                await this.loadBookings();
                form.reset();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        };

        // Render bookings list
        this.renderBookingsList();
    },

    renderBookingsList() {
        const grid = document.getElementById('bookingsGrid');
        grid.innerHTML = '';

        if (!this.state.bookings.length) {
            grid.innerHTML = '<p>No bookings yet</p>';
            return;
        }

        this.state.bookings.forEach(booking => {
            const slot = this.state.config?.ShiftSlots?.find(s => s.id === booking.shift_slot_id);
            const loc = this.state.config?.Locations?.find(l => l.id === booking.location_id);

            const card = document.createElement('div');
            card.className = 'card booking-item';
            card.innerHTML = `
                <h4>${new Date(booking.booking_date).toLocaleDateString()}</h4>
                <p><strong>Shift:</strong> ${slot?.name || 'Unknown'}</p>
                <p><strong>Location:</strong> ${loc?.name || 'Unknown'}</p>
                <p><strong>Status:</strong> <span class="badge badge-${booking.status === 'Confirmed' ? 'success' : 'warning'}">${booking.status}</span></p>
                <button class="btn btn-small btn-danger" onclick="App.cancelBooking(${booking.id})">Cancel</button>
            `;
            grid.appendChild(card);
        });
    },

    async cancelBooking(id) {
        if (confirm('Cancel this booking?')) {
            try {
                await API.bookings.cancel(id);
                alert('Booking cancelled');
                await this.loadBookings();
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
                document.querySelectorAll('.tab-content').forEach(t => t.classList.remove('active'));
                e.target.classList.add('active');
                document.getElementById(e.target.dataset.tab).classList.add('active');
            };
        });

        // Render pending users
        this.renderPendingUsersList();

        // Shift Slots
        const createShiftForm = document.getElementById('createShiftForm');
        createShiftForm.onsubmit = async (e) => {
            e.preventDefault();

            const name = document.getElementById('slotName').value;
            const startTime = document.getElementById('startTime').value;
            const endTime = document.getElementById('endTime').value;
            const freezeTime = document.getElementById('freezeTime').value || null;

            try {
                await API.shiftSlots.create(name, startTime, endTime, freezeTime);
                alert('Shift slot created!');
                await this.loadConfig();
                this.renderShiftSlotsList();
                createShiftForm.reset();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        };

        this.renderShiftSlotsList();

        // Locations
        const createLocForm = document.getElementById('createLocationForm');
        createLocForm.onsubmit = async (e) => {
            e.preventDefault();

            const name = document.getElementById('locName').value;
            const address = document.getElementById('locAddress').value;

            try {
                await API.locations.create(name, address);
                alert('Location created!');
                await this.loadConfig();
                this.renderLocationsList();
                createLocForm.reset();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        };

        this.renderLocationsList();

        // Rosters
        this.renderRostersList();
    },

    renderShiftSlotsList() {
        const list = document.getElementById('shiftSlotsList');
        list.innerHTML = '';

        this.state.config?.ShiftSlots?.forEach(slot => {
            const item = document.createElement('div');
            item.className = 'list-item';
            item.innerHTML = `
                <div>
                    <strong>${slot.name}</strong><br>
                    ${this.formatTime(slot.start_time)} - ${this.formatTime(slot.end_time)}
                    ${slot.freeze_time ? `<br>Freezes at: ${this.formatTime(slot.freeze_time)}` : ''}
                </div>
                <div>
                    <button class="btn btn-small btn-danger" onclick="App.deleteShiftSlot(${slot.id})">Delete</button>
                </div>
            `;
            list.appendChild(item);
        });
    },

    renderLocationsList() {
        const list = document.getElementById('locationsList');
        list.innerHTML = '';

        this.state.config?.Locations?.forEach(loc => {
            const item = document.createElement('div');
            item.className = 'list-item';
            item.innerHTML = `
                <div>
                    <strong>${loc.name}</strong><br>
                    ${loc.address || 'No address'}
                </div>
                <div>
                    <button class="btn btn-small btn-danger" onclick="App.deleteLocation(${loc.id})">Delete</button>
                </div>
            `;
            list.appendChild(item);
        });
    },

    renderRostersList() {
        const list = document.getElementById('rostersList');
        list.innerHTML = '';

        this.state.rosters?.forEach(roster => {
            const slot = this.state.config?.ShiftSlots?.find(s => s.id === roster.shift_slot_id);
            const item = document.createElement('div');
            item.className = 'list-item';
            item.innerHTML = `
                <div>
                    <strong>${slot?.name || 'Unknown'}</strong> - ${new Date(roster.roster_date).toLocaleDateString()}<br>
                    Status: <span class="badge badge-${roster.status === 'Open' ? 'primary' : 'success'}">${roster.status}</span>
                </div>
                <div>
                    ${roster.status === 'Open' ? `<button class="btn btn-small btn-warning" onclick="App.freezeRoster(${roster.id})">Freeze</button>` : ''}
                    ${roster.status !== 'Open' ? `<button class="btn btn-small btn-info" onclick="App.exportRoster(${roster.id})">Export</button>` : ''}
                </div>
            `;
            list.appendChild(item);
        });
    },

    renderPendingUsersList() {
        const list = document.getElementById('pendingUsersList');
        if (!list) return;

        list.innerHTML = '';

        if (!this.state.pendingUsers || this.state.pendingUsers.length === 0) {
            list.innerHTML = '<p style="color: #999; text-align: center; padding: 2rem;">No pending approvals</p>';
            return;
        }

        this.state.pendingUsers.forEach(user => {
            const item = document.createElement('div');
            item.className = 'list-item';
            const addressStatus = user.address_status === 'approved'
                ? '✅ Addresses approved'
                : '⏳ Addresses pending';

            item.innerHTML = `
                <div style="flex: 1;">
                    <strong>${user.name || 'N/A'}</strong><br>
                    ${user.email}<br>
                    ${user.phone_number ? `📞 ${user.phone_number}<br>` : ''}
                    <div style="margin-top: 0.5rem; background: #f0f0f0; padding: 0.5rem; border-radius: 4px; font-size: 0.85rem;">
                        <strong>📍 Pickup:</strong> ${user.pickup_address || 'Not provided'}<br>
                        <strong>📍 Dropoff:</strong> ${user.dropoff_address || 'Not provided'}<br>
                        <small style="color: #666;">${addressStatus}</small>
                    </div>
                    <small style="color: #999; margin-top: 0.5rem;">Requested: ${new Date(user.created_at).toLocaleDateString()}</small>
                </div>
                <div style="display: flex; flex-direction: column; gap: 0.5rem;">
                    <button class="btn btn-small btn-success" onclick="App.approveUser(${user.id})">Approve User</button>
                    <button class="btn btn-small btn-info" onclick="App.approveUserAddresses(${user.id})">Approve Addresses</button>
                    <button class="btn btn-small btn-danger" onclick="App.rejectUser(${user.id})">Reject</button>
                </div>
            `;
            list.appendChild(item);
        });
    },

    async deleteShiftSlot(id) {
        if (confirm('Delete this shift slot?')) {
            try {
                await API.shiftSlots.delete(id);
                alert('Shift slot deleted');
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
                alert('Location deleted');
                await this.loadConfig();
                this.renderLocationsList();
            } catch (error) {
                alert('Error: ' + error.message);
            }
        }
    },

    async freezeRoster(id) {
        if (confirm('Freeze this roster?')) {
            try {
                await API.rosters.freeze(id);
                alert('Roster frozen');
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

    async approveUser(id) {
        if (confirm('Approve this user?')) {
            try {
                await API.users.approve(id);
                alert('User approved!');
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
                alert('User rejected');
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
                alert('Addresses approved!');
                await this.loadPendingUsers();
                this.renderPendingUsersList();
            } catch (error) {
                alert('Error: ' + error.message);
            }
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
    },

    setupEventListeners() {
        // Global event listeners if needed
    }
};

// Initialize app when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    App.init();
    registerServiceWorker();
});

// PWA Service Worker Registration
function registerServiceWorker() {
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('/service-worker.js')
            .then(registration => {
                console.log('Service Worker registered:', registration);

                // Check for updates
                registration.addEventListener('updatefound', () => {
                    const newWorker = registration.installing;
                    newWorker.addEventListener('statechange', () => {
                        if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                            console.log('New service worker available - update available');
                            // Optionally show update notification to user
                            showUpdateNotification();
                        }
                    });
                });
            })
            .catch(error => {
                console.error('Service Worker registration failed:', error);
            });

        // Listen for message from service worker
        navigator.serviceWorker.addEventListener('message', event => {
            if (event.data.type === 'SW_ACTIVATED') {
                console.log('Service Worker activated');
            }
        });
    }
}

// Show update notification
function showUpdateNotification() {
    const message = document.createElement('div');
    message.style.cssText = `
        position: fixed;
        bottom: 1rem;
        left: 1rem;
        right: 1rem;
        background: #10b981;
        color: white;
        padding: 1rem;
        border-radius: 8px;
        z-index: 1000;
        display: flex;
        justify-content: space-between;
        align-items: center;
        box-shadow: 0 4px 12px rgba(0,0,0,0.2);
    `;
    message.innerHTML = `
        <span>📦 Update available - Reload to get the latest version</span>
        <button style="background: white; color: #10b981; border: none; padding: 0.5rem 1rem; border-radius: 4px; cursor: pointer; font-weight: 600;" onclick="window.location.reload()">Reload</button>
    `;
    document.body.appendChild(message);
}

// Handle install prompt
window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    console.log('Install prompt available');
    // Store event for later use
    window.installPrompt = e;
});

window.addEventListener('appinstalled', () => {
    console.log('PWA installed');
    window.installPrompt = null;
});
