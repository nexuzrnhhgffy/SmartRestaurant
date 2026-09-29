// ============================================================
//  Aurora — Shared core: API client, auth storage, UI helpers
// ============================================================
'use strict';

const API = {
  base: '',
  token: () => localStorage.getItem('aurora_token') || sessionStorage.getItem('aurora_token') || '',

  async request(method, url, body, anonymous = false) {
    const headers = { 'Content-Type': 'application/json' };
    if (!anonymous && this.token()) headers['Authorization'] = `Bearer ${this.token()}`;
    const res = await fetch(url, {
      method, headers,
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    if (res.status === 401 && !anonymous) {
      // token expired → back to login (except on public pages)
      if (location.pathname.includes('admin')) { Auth.logout(); throw new Error('Session expired'); }
    }
    if (!res.ok) {
      let msg = `Request failed (${res.status})`;
      try { const problem = await res.json(); msg = (problem.detail || problem.title || problem.message) || msg; } catch (_) {}
      throw new Error(msg);
    }
    if (res.status === 204) return null;
    return res.json();
  },
  get(url, anonymous) { return this.request('GET', url, undefined, anonymous); },
  post(url, body, anonymous) { return this.request('POST', url, body, anonymous); },
  put(url, body, anonymous) { return this.request('PUT', url, body, anonymous); },
  del(url, anonymous) { return this.request('DELETE', url, undefined, anonymous); }
};

const Auth = {
  save(res) {
    (location.pathname.includes('admin') ? localStorage : sessionStorage).setItem('aurora_token', res.token);
    (location.pathname.includes('admin') ? localStorage : sessionStorage).setItem('aurora_user', JSON.stringify(res.user));
  },
  user() {
    try {
      const raw = localStorage.getItem('aurora_user') || sessionStorage.getItem('aurora_user');
      return raw ? JSON.parse(raw) : null;
    } catch (_) { return null; }
  },
  role() { return this.user()?.role ?? null; },
  logout() { ['aurora_token', 'aurora_user'].forEach(k => { localStorage.removeItem(k); sessionStorage.removeItem(k); }); }
};

// ---------- formatting ----------
const fmt = {
  money: v => '$' + (Number(v) || 0).toFixed(2),
  dt: iso => iso ? new Date(iso).toLocaleString([], { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—',
  d: iso => iso ? new Date(iso).toLocaleDateString([], { month: 'short', day: 'numeric', year: 'numeric' }) : '—',
  time: iso => iso ? new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '—',
  mins: m => m >= 60 ? `${Math.floor(m / 60)}h ${m % 60}m` : `${m}m`
};

const ROLE_NAMES = { 1: 'SuperAdmin', 2: 'Manager', 3: 'Cashier', 4: 'Waiter', 5: 'Chef', 6: 'Accountant', 7: 'Customer' };

// ---------- toasts ----------
function toast(title, msg = '', type = '') {
  const wrap = document.getElementById('toasts');
  if (!wrap) return alert(`${title}${msg ? ' — ' + msg : ''}`);
  const el = document.createElement('div');
  el.className = `toast ${type}`;
  el.innerHTML = `<b></b><span></span>`;
  el.querySelector('b').textContent = title;
  el.querySelector('span').textContent = msg;
  wrap.appendChild(el);
  setTimeout(() => { el.style.opacity = '0'; el.style.transition = 'opacity .4s'; setTimeout(() => el.remove(), 400); }, 3800);
}

// ---------- initials avatar colors ----------
function avatarColor(seedStr) {
  const colors = ['#4f46e5', '#0ea5a4', '#f59e0b', '#ec4899', '#8b5cf6', '#3b82f6', '#ef4444', '#22c55e'];
  let h = 0;
  for (const ch of String(seedStr || '?')) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return colors[h % colors.length];
}
function initials(name) {
  return String(name || '?').split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
}

// ---------- status badge helper ----------
function badge(statusName) {
  const cls = { Available: 'b-green', Occupied: 'b-indigo', Reserved: 'b-amber', Dirty: 'b-red', OutOfService: 'b-gray',
    Pending: 'b-amber', Confirmed: 'b-blue', Preparing: 'b-blue', Ready: 'b-indigo', Served: 'b-teal', Completed: 'b-green', Cancelled: 'b-red',
    Unpaid: 'b-amber', PartiallyPaid: 'b-blue', Paid: 'b-green', Refunded: 'b-gray',
    Approved: 'b-green', Rejected: 'b-red', Scheduled: 'b-amber', InProgress: 'b-blue', Absent: 'b-red',
    Inquiry: 'b-amber', Quoted: 'b-blue', Seated: 'b-green', NoShow: 'b-red', Draft: 'b-gray', Ordered: 'b-blue', Received: 'b-green' }[statusName] || 'b-gray';
  return `<span class="badge ${cls}"><span class="dot"></span>${statusName}</span>`;
}

const $ = sel => document.querySelector(sel);
const $$ = sel => document.querySelectorAll(sel);
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
function money(v) { return fmt.money(v); }
