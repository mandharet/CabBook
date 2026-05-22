// API wrapper for CabBook backend
const API = {
    baseUrl: '/api',
    token: localStorage.getItem('token'),

    async request(method, path, data = null) {
        const url = `${this.baseUrl}${path}`;
        const options = {
            method,
            headers: {
                'Content-Type': 'application/json',
            }
        };

        if (this.token) {
            options.headers['Authorization'] = `Bearer ${this.token}`;
        }

        if (data) {
            options.body = JSON.stringify(data);
        }

        try {
            const response = await fetch(url, options);

            if (response.status === 401) {
                localStorage.removeItem('token');
                window.location.hash = '#/';
                throw new Error('Unauthorized');
            }

            const contentType = response.headers.get('content-type');
            if (!contentType || !contentType.includes('application/json')) {
                return response;
            }

            const body = await response.json();

            if (!response.ok) {
                throw new Error(body.error || `HTTP ${response.status}`);
            }

            return body;
        } catch (error) {
            console.error('API Error:', error);
            throw error;
        }
    },

    // Auth endpoints
    auth: {
        sendOtp(email, tenantId) {
            return API.request('POST', '/auth/send-otp', { email, tenantId });
        },

        verifyOtp(email, otp, tenantId) {
            return API.request('POST', '/auth/verify-otp', { email, otp, tenantId });
        },

        signup(email, phoneNumber, name, pickupAddress, dropoffAddress, tenantId) {
            return API.request('POST', '/auth/signup', { email, phoneNumber, name, pickupAddress, dropoffAddress, tenantId });
        }
    },

    // Booking endpoints
    bookings: {
        create(shiftSlotId, locationId, bookingDate) {
            return API.request('POST', '/booking', { shiftSlotId, locationId, bookingDate });
        },

        list() {
            return API.request('GET', '/booking');
        },

        get(id) {
            return API.request('GET', `/booking/${id}`);
        },

        cancel(id) {
            return API.request('POST', `/booking/${id}/cancel`);
        }
    },

    // Roster endpoints
    rosters: {
        list(fromDate, toDate) {
            const query = new URLSearchParams({
                fromDate: fromDate?.toISOString().split('T')[0],
                toDate: toDate?.toISOString().split('T')[0]
            });
            return API.request('GET', `/roster?${query}`);
        },

        get(id) {
            return API.request('GET', `/roster/${id}`);
        },

        freeze(id) {
            return API.request('POST', `/roster/${id}/freeze`);
        },

        export(id) {
            return API.request('GET', `/roster/${id}/export`);
        }
    },

    // Tenant endpoints
    tenant: {
        getConfig() {
            return API.request('GET', '/tenant/config');
        },

        get() {
            return API.request('GET', '/tenant');
        },

        update(name, timeZone) {
            return API.request('PUT', '/tenant', { name, timeZone });
        }
    },

    // ShiftSlot endpoints
    shiftSlots: {
        list(activeOnly = true) {
            const query = activeOnly ? '?activeOnly=true' : '';
            return API.request('GET', `/shiftslot${query}`);
        },

        get(id) {
            return API.request('GET', `/shiftslot/${id}`);
        },

        create(name, startTime, endTime, freezeTime) {
            return API.request('POST', '/shiftslot', { name, startTime, endTime, freezeTime });
        },

        update(id, name, startTime, endTime, freezeTime) {
            return API.request('PUT', `/shiftslot/${id}`, { name, startTime, endTime, freezeTime });
        },

        delete(id) {
            return API.request('DELETE', `/shiftslot/${id}`);
        },

        setFreezeOverride(id, overrideDate, freezeTime) {
            return API.request('POST', `/shiftslot/${id}/freeze-override`, { overrideDate, freezeTime });
        }
    },

    // Location endpoints
    locations: {
        list(activeOnly = true) {
            const query = activeOnly ? '?activeOnly=true' : '';
            return API.request('GET', `/location${query}`);
        },

        get(id) {
            return API.request('GET', `/location/${id}`);
        },

        create(name, address) {
            return API.request('POST', '/location', { name, address });
        },

        update(id, name, address) {
            return API.request('PUT', `/location/${id}`, { name, address });
        },

        delete(id) {
            return API.request('DELETE', `/location/${id}`);
        }
    },

    // User endpoints (admin)
    users: {
        getPending() {
            return API.request('GET', '/user/pending');
        },

        approve(userId) {
            return API.request('POST', `/user/${userId}/approve`);
        },

        reject(userId) {
            return API.request('POST', `/user/${userId}/reject`);
        },

        approveAddresses(userId) {
            return API.request('POST', `/user/${userId}/approve-addresses`);
        },

        updateAddresses(userId, pickupAddress, dropoffAddress) {
            return API.request('PUT', `/user/${userId}/addresses`, { pickupAddress, dropoffAddress });
        },

        get(userId) {
            return API.request('GET', `/user/${userId}`);
        }
    },

    setToken(token) {
        this.token = token;
        localStorage.setItem('token', token);
    },

    logout() {
        this.token = null;
        localStorage.removeItem('token');
    }
};
