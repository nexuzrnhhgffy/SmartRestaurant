// ============================================================
//  Aurora Admin — Part 1: shell, login, router, overview,
//  POS (tables), orders, kitchen quick view
// ============================================================
'use strict';

const Admin = {
  user: null,
  current: 'overview',
  pollTimer: null,

  // ---------------- boot ----------------
  async boot() {
    this.user = Auth.user();
    const token = localStorage.getItem('aurora_token') || sessionStorage.getItem('aurora_token');
    if (!this.user || !token) { this.showLogin(); return; }
    try {
      const me = await API.get('/api/auth/me');
      this.user = me;
      this.enterApp();
    } catch (_) { this.showLogin(); }
  },

  showLogin() {
    $('#loginScreen').style.display = 'grid';
    $('#app').classList.remove('ready');
    const btn = $('#loginBtn');
    btn.onclick = async () => {
      btn.disabled = true; btn.textContent = 'Signing in…'; $('#loginErr').textContent = '';
      try {
        const res = await API.post('/api/auth/login', {
          email: $('#loginEmail').value.trim(), password: $('#loginPassword').value
        }, true);
        if (!res.success) { $('#loginErr').textContent = res.message; return; }
        localStorage.setItem('aurora_token', res.token);
        localStorage.setItem('aurora_user', JSON.stringify(res.user));
        this.user = res.user;
        this.enterApp();
      } catch (e) { $('#loginErr').textContent = e.message; }
      finally { btn.disabled = false; btn.textContent = 'Enter Dashboard'; }
    };
    $('#loginPassword').addEventListener('keydown', e => { if (e.key === 'Enter') btn.click(); });
  },

  enterApp() {
    $('#loginScreen').style.display = 'none';
    $('#app').classList.add('ready');
    const u = this.user;
    $('#tbName').textContent = u.fullName;
    $('#tbRole').textContent = ROLE_NAMES[u.role] || '—';
    $('#tbRoleName').textContent = ROLE_NAMES[u.role] || '—';
    const av = $('#tbAvatar');
    av.textContent = initials(u.fullName);
    av.style.background = avatarColor(u.fullName);
    this.buildSidebar();
    this.navigate(location.hash.replace('#', '') || 'overview');
    this.startNotifPolling();
  },

  // ---------------- sidebar / routing ----------------
  NAV: [
    { sec: 'Operations' },
    { id: 'overview', label: 'Overview', ico: '◈', roles: [1, 2, 6] },
    { id: 'pos', label: 'Floor & POS', ico: '▦', roles: [1, 2, 3, 4] },
    { id: 'orders', label: 'Orders', ico: '🧾', roles: [1, 2, 3, 4] },
    { id: 'kitchen', label: 'Kitchen', ico: '👨‍🍳', roles: [1, 2, 5] },
    { id: 'tables', label: 'Tables', ico: '🪑', roles: [1, 2] },
    { sec: 'Front of House' },
    { id: 'reservations', label: 'Reservations', ico: '📅', roles: [1, 2, 3, 4] },
    { id: 'menu', label: 'Menu Manager', ico: '📖', roles: [1, 2, 5] },
    { id: 'customers', label: 'Customers', ico: '🤝', roles: [1, 2, 3] },
    { sec: 'Growth' },
    { id: 'events', label: 'Events & Catering', ico: '🎉', roles: [1, 2, 6] },
    { id: 'reviews', label: 'Reviews', ico: '★', roles: [1, 2] },
    { id: 'coupons', label: 'Coupons', ico: '🏷', roles: [1, 2] },
    { sec: 'Back Office' },
    { id: 'inventory', label: 'Inventory', ico: '📦', roles: [1, 2] },
    { id: 'staff', label: 'Staff & Shifts', ico: '👔', roles: [1, 2] },
    { id: 'finance', label: 'Accounting', ico: '💰', roles: [1, 2, 6] },
    { id: 'settings', label: 'Settings', ico: '⚙', roles: [1] },
  ],

  buildSidebar() {
    const role = this.user.role;
    $('#sidebar').innerHTML = this.NAV.map(item => {
      if (item.sec) {
        const hasVisible = this.NAV.some(n => !n.sec && n.roles.includes(role));
        return `<div class="nav-label">${item.sec}</div>`;
      }
      return item.roles.includes(role)
        ? `<button class="nav-item" data-sec="${item.id}"><span class="n-ico">${item.ico}</span><span class="n-txt">${item.label}</span></button>`
        : '';
    }).join('');
    $('#sidebar').querySelectorAll('.nav-item').forEach(b => b.onclick = () => this.navigate(b.dataset.sec));
  },

  async navigate(sec) {
    this.current = sec;
    location.hash = sec;
    $('#sidebar').querySelectorAll('.nav-item').forEach(b => b.classList.toggle('active', b.dataset.sec === sec));
    clearInterval(this.pollTimer);
    const fn = this['render_' + sec];
    const main = $('#main');
    main.innerHTML = '<div class="card"><div class="skeleton" style="height:120px"></div></div>';
    if (typeof this['render_' + sec] === 'function') {
      try { await fn.call(this); } catch (e) { main.innerHTML = `<div class="empty"><div class="e-ico">⚠</div><b>Failed to load</b>${esc(e.message)}</div>`; }
    } else {
      main.innerHTML = `<div class="empty"><div class="e-ico">A</div><b>${sec}</b>Module coming soon.</div>`;
    }
  },

  head(title, sub, actionsHtml = '') {
    return `<div class="page-head"><div><h1>${title}</h1><div class="ph-sub">${sub}</div></div><div class="ph-actions">${actionsHtml}</div></div>`;
  },

  // ---------------- notifications ----------------
  async startNotifPolling() {
    const role = this.user.role;
    const check = async () => {
      try {
        const list = await API.get(`/api/notifications?role=${role}`);
        const unread = list.filter(n => !n.isRead).length;
        $('#notifDot').classList.toggle('on', unread > 0);
        $('#notifList').innerHTML = list.slice(0, 20).map(n => `
          <div class="np-item ${n.isRead ? '' : 'unread'}" data-id="${n.id}">
            <b>${esc(n.title)}</b><p>${esc(n.message)}</p><time>${fmt.dt(n.createdAt)}</time>
          </div>`).join('') || '<div class="np-item"><p>No notifications.</p></div>';
        $('#notifList').querySelectorAll('.np-item.unread').forEach(el => el.onclick = async () => {
          await API.post(`/api/notifications/${el.dataset.id}/read`); check();
        });
      } catch (_) {}
    };
    $('#notifBtn').onclick = () => $('#notifPanel').classList.toggle('open');
    $('#markAllRead').onclick = async () => { await API.post(`/api/notifications/read-all?role=${role}`); check(); };
    await check();
    this.pollTimer = setInterval(check, 25000);
  },

  // ================= OVERVIEW =================
  async render_overview() {
    const kpis = await API.get('/api/dashboard/kpis');
    const sales = await API.get('/api/dashboard/sales?days=7');
    $('#main').innerHTML = this.head('Operations Overview', `Welcome back, ${esc(this.user.fullName.split(' ')[0])} — here is the restaurant at a glance.`) + `
      <div class="kpi-grid">
        <div class="kpi accent"><div class="k-label">Revenue Today</div><div class="k-value">${money(kpis.revenueToday)}</div><div class="k-delta">${money(kpis.revenueMonth)} this month</div><div class="k-spark">$</div></div>
        <div class="kpi"><div class="k-label">Orders Today</div><div class="k-value">${kpis.ordersToday}</div><div class="k-delta flat">avg ticket ${money(kpis.avgOrderValue)}</div><div class="k-spark">O</div></div>
        <div class="kpi teal"><div class="k-label">Active Orders</div><div class="k-value">${kpis.activeOrders}</div><div class="k-delta flat">live on the floor & delivery</div><div class="k-spark">⏱</div></div>
        <div class="kpi"><div class="k-label">Reservations Today</div><div class="k-value">${kpis.reservationsToday}</div><div class="k-delta flat">${kpis.customersCount} loyalty members</div><div class="k-spark">📅</div></div>
        <div class="kpi ${kpis.lowStockAlerts > 0 ? 'amber' : ''}"><div class="k-label">Low Stock Alerts</div><div class="k-value">${kpis.lowStockAlerts}</div><div class="k-delta flat">food cost ${kpis.foodCostPct}% of revenue</div><div class="k-spark">📦</div></div>
        <div class="kpi ${kpis.newEventInquiries > 0 ? 'rose' : ''}"><div class="k-label">Event Inquiries</div><div class="k-value">${kpis.newEventInquiries}</div><div class="k-delta flat">${kpis.pendingReviews} reviews pending</div><div class="k-spark">🎉</div></div>
      </div>
      <div class="grid-21 mb">
        <div class="card"><div class="card-title"><h3>Daily Revenue — last 7 days</h3></div><div class="chart-box"><canvas id="chDaily"></canvas></div></div>
        <div class="card"><div class="card-title"><h3>Order Types</h3></div><div class="chart-box"><canvas id="chTypes"></canvas></div>${legendHtml(sales.orderTypes)}</div>
      </div>
      <div class="grid-2">
        <div class="card"><div class="card-title"><h3>Top Sellers by Revenue</h3></div><div class="chart-box"><canvas id="chTop" height="260"></canvas></div></div>
        <div class="card"><div class="card-title"><h3>Payment Mix</h3><small>last 7 days</small></div><div class="chart-box"><canvas id="chPay"></canvas></div>${legendHtml(sales.paymentMethods)}</div>
      </div>`;
    Charts.bars($('#chDaily'), sales.dailyRevenue, { currency: true, color: '#4f46e5' });
    Charts.donut($('#chTypes'), sales.orderTypes.map(o => ({ label: o.category, value: o.amount })));
    Charts.bars($('#chTop'), sales.topItems.map(t => ({ label: t.name.split(' ')[0], value: t.revenue })), { color: '#f59e0b', currency: true });
    Charts.donut($('#chPay'), sales.paymentMethods.map(o => ({ label: o.category, value: o.amount })), { center: 'pay' });
  },

  // ================= POS / FLOOR =================
  async render_pos() {
    const [tables, stats] = await Promise.all([API.get('/api/tables'), API.get('/api/orders/stats')]);
    const sections = ['All', 'MainHall', 'Terrace', 'VIP', 'Private', 'Bar'];
    $('#main').innerHTML = this.head('Floor & POS', 'Tap a table to view its bill, add items or take payment.',
      `<button class="btn outline" onclick="Admin.openTableMap()">↻ Refresh</button>
       <button class="btn primary" onclick="Admin.openWalkIn()">+ Walk-in / New Order</button>`) + `
      <div class="kpi-grid" style="grid-template-columns:repeat(auto-fit,minmax(160px,1fr))">
        <div class="kpi"><div class="k-label">Pending</div><div class="k-value">${stats.pending}</div></div>
        <div class="kpi"><div class="k-label">In Kitchen</div><div class="k-value">${stats.preparing}</div></div>
        <div class="kpi"><div class="k-label">Ready</div><div class="k-value">${stats.readyToday}</div></div>
        <div class="kpi"><div class="k-label">Completed</div><div class="k-value">${stats.completedToday}</div></div>
        <div class="kpi teal"><div class="k-label">Revenue</div><div class="k-value">${money(stats.revenueToday)}</div></div>
      </div>
      <div class="filter-bar" id="secFilter">
        ${sections.map((s, i) => `<button class="btn sm ${i === 0 ? 'soft' : 'outline'}" data-sec="${s}">${s}</button>`).join('')}
      </div>
      <div class="floor-grid" id="floorGrid"></div>`;
    const draw = (secName = 'All') => {
      const list = secName === 'All' ? tables : tables.filter(t => t.sectionName === secName);
      $('#floorGrid').innerHTML = list.map(t => `
        <div class="table-tile st-${t.statusName}" onclick="Admin.openTable(${t.id})">
          <span class="t-sec">${t.sectionName}</span>
          <div class="t-num">T${t.number}</div>
          <div class="t-cap">${t.capacity} seats</div>
          <div class="t-order">${badge(t.statusName)}</div>
        </div>`).join('');
    };
    draw();
    $('#secFilter').querySelectorAll('button').forEach(b => b.onclick = () => {
      $('#secFilter').querySelectorAll('button').forEach(x => x.className = 'btn sm outline');
      b.className = 'btn sm soft';
      draw(b.dataset.sec);
    });
    this._tables = tables;
  },

  openTableMap() { this.render_pos(); },

  async openTable(tableId) {
    const t = this._tables?.find(x => x.id === tableId);
    const active = await API.get('/api/orders?status=1') // pending
      .then(l => l.filter(o => o.tableId === tableId));
    const allToday = await API.get(`/api/orders`);
    const tableOrder = allToday.find(o => o.tableId === tableId && [1, 2, 3, 4, 5].includes(o.status));
    openModal(`
      <div class="modal-head"><h3>Table T${t?.number ?? '?'} ${t ? '· ' + t.sectionName + ' · ' + t.capacity + ' seats' : ''}</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="flex wrap mb">
        ${badge(t?.statusName || 'Available')}
        <span class="muted">QR token:</span> <span class="mono" style="font-size:.8rem">${esc(t?.qrToken || '')}</span>
      </div>
      ${tableOrder ? `
        <div class="card mb" style="background:#f8f9fe">
          <div class="flex" style="justify-content:space-between">
            <b>${esc(tableOrder.orderNumber)}</b>${badge(tableOrder.statusName)}
          </div>
          <div class="muted" style="font-size:.82rem;margin:6px 0">${tableOrder.items.length} items · opened ${fmt.dt(tableOrder.createdAt)}</div>
          <div class="flex" style="justify-content:space-between;font-size:1.1rem"><b>Total</b><b>${money(tableOrder.total)}</b></div>
        </div>
        <div class="flex wrap">
          <button class="btn primary" onclick="Admin.openOrderView(${tableOrder.id})">Open Bill</button>
          <button class="btn success" onclick="Admin.openPayment(${tableOrder.id})">Take Payment</button>
          <button class="btn outline" onclick="Admin.setTableStatus(${tableId}, 4)">Mark Dirty →</button>
        </div>`
        : `
        <div class="empty" style="padding:26px"><b>No open order on this table</b>
          <div class="flex" style="justify-content:center;margin-top:14px;flex-wrap:wrap">
            <button class="btn primary" onclick="Admin.openNewOrder({tableId:${tableId}})">Start New Order</button>
            <button class="btn outline" onclick="Admin.setTableStatus(${tableId}, 1)">Mark Available</button>
            <button class="btn outline" onclick="Admin.setTableStatus(${tableId}, 5)">Out of Service</button>
          </div>
        </div>`}
    `);
  },

  async setTableStatus(id, status) {
    await API.post(`/api/tables/${id}/status`, { status });
    closeModal(); toast('Table updated', '', 'success'); this.render_pos();
  },

  async openWalkIn() {
    const tables = (await API.get('/api/tables')).filter(t => t.statusName === 'Available');
    openModal(`
      <div class="modal-head"><h3>New Walk-in Order</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="field"><label>Order type</label>
        <select id="wiType"><option value="1">Dine-in</option><option value="2">Takeaway</option><option value="3">Delivery</option><option value="4">Pickup</option></select></div>
      <div id="wiTableWrap" class="field"><label>Table</label>
        <select id="wiTable">${tables.map(t => `<option value="${t.id}">T${t.number} — ${t.sectionName} (${t.capacity} seats)</option>`).join('') || '<option value="">No free tables</option>'}</select></div>
      <div class="field"><label>Guest name / phone</label><input id="wiName" placeholder="Walk-in guest"><input id="wiPhone" placeholder="Phone (optional)" style="margin-top:8px"></div>
      <div class="modal-actions">
        <button class="btn outline" onclick="closeModal()">Cancel</button>
        <button class="btn primary" onclick="Admin.createWalkIn()">Continue → Add Items</button>
      </div>`);
    $('#wiType').onchange = e => $('#wiTableWrap').style.display = e.target.value === '1' ? 'flex' : 'none';
  },

  async createWalkIn() {
    const type = +$('#wiType').value;
    const order = await API.post('/api/orders', {
      type, tableId: type === 1 ? +$('#wiTable').value : null,
      guestName: $('#wiName').value.trim() || null, phone: $('#wiPhone').value.trim() || null,
      items: []
    });
    closeModal();
    toast('Order created', order.orderNumber, 'success');
    this.openNewOrder({ orderId: order.id, tableId: order.tableId });
  },

  // ---------------- add items to order (menu picker) ----------------
  async openNewOrder({ orderId, tableId } = {}) {
    const menu = await API.get('/api/menu?includeUnavailable=true');
    const cats = await API.get('/api/menu/categories');
    window._newOrder = { orderId, tableId, items: [] };
    openModal(`
      <div class="modal-head"><h3>${orderId ? 'Add items to order' : 'New order'}</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="filter-bar" style="margin-bottom:10px">
        <button class="btn sm soft" data-c="0">All</button>
        ${cats.map(c => `<button class="btn sm outline" data-c="${c.id}">${esc(c.name)}</button>`).join('')}
      </div>
      <div class="menu-mgr-grid" id="pickGrid" style="max-height:46dvh;overflow:auto"></div>
      <div class="mt flex" style="justify-content:space-between">
        <span id="pickSummary" class="muted"></span>
        <button class="btn primary" onclick="Admin.submitNewOrder()">Send to Kitchen</button>
      </div>`, 'wide');
    const draw = (cat = 0) => {
      const list = cat ? menu.filter(m => m.categoryId === cat) : menu;
      $('#pickGrid').innerHTML = list.map(m => `
        <div class="mi-card ${m.isAvailable ? '' : 'unavailable'}" style="cursor:pointer" onclick='Admin.pickItem(${JSON.stringify({ id: m.id, name: m.name, price: m.price }).replace(/'/g, '&#39;')})'>
          <div class="mi-img">${esc(m.name.split(' ').map(w => w[0]).slice(0, 2).join(''))}</div>
          <div class="mi-body">
            <div class="mi-name">${esc(m.name)}</div>
            <div class="mi-cat">${esc(m.categoryName)} · ${m.prepTimeMinutes}min</div>
            <div class="mi-foot"><span class="mi-price">${money(m.price)}</span>${m.isAvailable ? '' : '<span class="badge b-red">Sold out</span>'}</div>
          </div>
        </div>`).join('');
      $('#pickGrid').querySelectorAll('[data-c]') // rebind cat filter
      document.querySelectorAll('[data-c]').forEach(b => b.onclick = () => {
        document.querySelectorAll('[data-c]').forEach(x => { x.className = 'btn sm outline'; });
        b.className = 'btn sm soft';
        draw(+b.dataset.c);
      });
      this.updatePickSummary();
    };
    draw();
  },

  pickItem(item) {
    const no = window._newOrder;
    const ex = no.items.find(i => i.menuItemId === item.id);
    if (ex) ex.quantity++;
    else no.items.push({ menuItemId: item.id, name: item.name, price: item.price, quantity: 1 });
    this.updatePickSummary();
  },

  updatePickSummary() {
    const no = window._newOrder;
    if (!no) return;
    const qty = no.items.reduce((s, i) => s + i.quantity, 0);
    $('#pickSummary').innerHTML = qty ? `<b>${qty} items</b> selected — ${money(no.items.reduce((s, i) => s + i.price * i.quantity, 0))}` : 'Tap dishes to add them';
  },

  async submitNewOrder() {
    const no = window._newOrder;
    if (!no.items.length) return toast('No items', 'Add at least one dish', 'error');
    if (no.orderId) {
      for (const item of no.items) await API.post(`/api/orders/${no.orderId}/items`, item);
      closeModal();
      toast('Items added', '', 'success');
      this.openOrderView(no.orderId);
      if (this.current === 'pos') this.render_pos();
    } else {
      const order = await API.post('/api/orders', { type: 1, tableId: no.tableId, items: no.items });
      closeModal();
      toast('Order sent to kitchen', order.orderNumber, 'success');
      this.render_pos();
    }
  },

  // ---------------- order detail / bill ----------------
  async openOrderView(orderId) {
    const o = await API.get(`/api/orders/${orderId}`);
    openModal(`
      <div class="modal-head"><h3>${esc(o.orderNumber)} ${badge(o.statusName)}</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="muted mb" style="font-size:.84rem">
        ${o.typeName} ${o.tableNumber ? '· Table T' + o.tableNumber : ''} ${o.customerName ? '· ' + esc(o.customerName) : ''} · opened ${fmt.dt(o.createdAt)}
        ${o.createdByName ? '· by ' + esc(o.createdByName) : ''}
      </div>
      ${o.items.length ? `
      <div class="tbl-wrap mb"><table class="data" style="min-width:0">
        <thead><tr><th>Item</th><th class="num">Qty</th><th class="num">Price</th><th class="num">Total</th><th>Status</th></tr></thead>
        <tbody>${o.items.map(i => `
          <tr><td><b>${esc(i.name)}</b>${i.modifierText ? `<div class="muted" style="font-size:.74rem">${esc(i.modifierText)}</div>` : ''}${i.notes ? `<div class="muted" style="font-size:.74rem;font-style:italic">${esc(i.notes)}</div>` : ''}</td>
            <td class="num">${i.quantity}</td><td class="num">${money(i.unitPrice)}</td><td class="num">${money(i.lineTotal)}</td>
            <td>${badge(i.statusName)}</td></tr>`).join('')}
        </tbody></table></div>` : '<div class="empty mb">No items yet — use “Add items”.</div>'}
      <div class="card mb" style="background:#f8f9fe">
        <div class="flex" style="justify-content:space-between"><span class="muted">Subtotal</span><span>${money(o.subtotal)}</span></div>
        ${o.discountAmount ? `<div class="flex" style="justify-content:space-between;color:#15803d"><span>Discount ${o.couponCode ? '(' + o.couponCode + ')' : ''}</span><span>-${money(o.discountAmount)}</span></div>` : ''}
        <div class="flex" style="justify-content:space-between"><span class="muted">Tax (9%)</span><span>${money(o.taxAmount)}</span></div>
        ${o.serviceCharge ? `<div class="flex" style="justify-content:space-between"><span class="muted">Service 5%</span><span>${money(o.serviceCharge)}</span></div>` : ''}
        ${o.deliveryFee ? `<div class="flex" style="justify-content:space-between"><span class="muted">Delivery</span><span>${money(o.deliveryFee)}</span></div>` : ''}
        ${o.tip ? `<div class="flex" style="justify-content:space-between"><span class="muted">Tip</span><span>${money(o.tip)}</span></div>` : ''}
        <div class="flex mt" style="justify-content:space-between;font-size:1.15rem"><b>Total</b><b>${money(o.total)}</b></div>
        <div class="flex" style="justify-content:space-between"><span class="muted">Payment</span>${badge(o.paymentStatusName)}</div>
      </div>
      ${o.payments.length ? `<div class="muted mb" style="font-size:.8rem">Payments: ${o.payments.map(p => `${p.methodName} ${money(p.amount)}${p.cashierName ? ' (' + esc(p.cashierName) + ')' : ''}`).join(' · ')}</div>` : ''}
      <div class="flex wrap">
        ${o.status === 'Pending' ? '<button class="btn primary" onclick="Admin.setOrderStatus(' + o.id + ',2)">Confirm</button>' : ''}
        <button class="btn outline" onclick="Admin.openNewOrder({orderId:${o.id}})">+ Add Items</button>
        ${[2, 3].includes(o.status) ? '<button class="btn outline" onclick="Admin.setOrderStatus(' + o.id + ',4)">Mark Ready</button>' : ''}
        ${o.status === 'Ready' ? '<button class="btn outline" onclick="Admin.setOrderStatus(' + o.id + ',5)">Mark Served</button>' : ''}
        ${o.status === 'Served' || o.paymentStatus === 'Paid' ? '<button class="btn success" onclick="Admin.setOrderStatus(' + o.id + ',6)">Complete</button>' : ''}
        ${o.paymentStatus !== 'Paid' && ![6, 7].includes(o.status) ? '<button class="btn success" onclick="Admin.openPayment(' + o.id + ')">Take Payment</button>' : ''}
        ${![6, 7].includes(o.status) ? '<button class="btn danger" onclick="Admin.cancelOrder(' + o.id + ')">Cancel</button>' : ''}
        <span class="spacer"></span>
        <button class="btn outline" onclick="window.print()">🖨 Print</button>
      </div>`, 'wide');
  },

  async setOrderStatus(id, status) {
    await API.post(`/api/orders/${id}/status`, { status });
    closeModal(); toast('Order updated', '', 'success');
    if (this.current === 'orders') this.render_orders();
    if (this.current === 'pos') this.render_pos();
  },

  async cancelOrder(id) {
    if (!confirm('Cancel this order?')) return;
    await API.post(`/api/orders/${id}/cancel?reason=Cancelled from dashboard`, {});
    closeModal(); toast('Order cancelled', '', 'success');
    this.navigate(this.current);
  },

  // ---------------- payment ----------------
  async openPayment(orderId) {
    const o = await API.get(`/api/orders/${orderId}`);
    const paid = o.payments.reduce((s, p) => s + p.amount, 0);
    const due = Math.max(0, o.total - paid);
    openModal(`
      <div class="modal-head"><h3>Payment — ${esc(o.orderNumber)}</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="kpi-grid" style="grid-template-columns:1fr 1fr;margin-bottom:16px">
        <div class="fin-tile"><div class="f-val">${money(o.total)}</div><div class="f-lab">Total</div></div>
        <div class="fin-tile pos"><div class="f-val">${money(due)}</div><div class="f-lab">Due</div></div>
      </div>
      <div class="field"><label>Method</label>
        <select id="payMethod"><option value="1">Cash</option><option value="2">Card</option><option value="3">Online</option><option value="4">Wallet</option><option value="5">QR Code</option></select></div>
      <div class="field"><label>Amount received</label><input type="number" step="0.01" id="payAmount" value="${due.toFixed(2)}"></div>
      <div class="field"><label>Tip</label><input type="number" step="0.01" id="payTip" value="0"></div>
      <div class="flex mb"><span class="muted">Cash change:</span><b id="payChange" class="mono">$0.00</b></div>
      <div class="modal-actions">
        <button class="btn outline" onclick="closeModal()">Cancel</button>
        <button class="btn success" id="payBtn">Complete Payment</button>
      </div>`);
    const upd = () => {
      const amt = parseFloat($('#payAmount').value || 0), tip = parseFloat($('#payTip').value || 0);
      $('#payChange').textContent = money(Math.max(0, amt - tip - due));
    };
    $('#payAmount').addEventListener('input', upd); $('#payTip').addEventListener('input', upd);
    $('#payBtn').onclick = async () => {
      try {
        await API.post('/api/payments', { orderId, method: +$('#payMethod').value, amount: parseFloat($('#payAmount').value), tip: parseFloat($('#payTip').value || 0) });
        closeModal(); toast('Payment complete', '', 'success');
        this.render_pos();
      } catch (e) { toast('Payment failed', e.message, 'error'); }
    };
  },

  // ================= ORDERS LIST =================
  async render_orders() {
    const statusSel = `<select id="ordStatus"><option value="">All statuses</option>
      <option value="1">Pending</option><option value="2">Confirmed</option><option value="3">Preparing</option>
      <option value="4">Ready</option><option value="5">Served</option><option value="6">Completed</option><option value="7">Cancelled</option></select>`;
    const typeSel = `<select id="ordType"><option value="">All types</option>
      <option value="1">Dine-in</option><option value="2">Takeaway</option><option value="3">Delivery</option><option value="4">Pickup</option></select>`;
    $('#main').innerHTML = this.head('Orders', 'Every dine-in, delivery, pickup and takeaway ticket.') + `
      <div class="filter-bar">
        ${statusSel}${typeSel}
        <input id="ordSearch" placeholder="Search #, name, phone…" style="flex:1;min-width:180px">
        <button class="btn soft" onclick="Admin.render_orders()">Search</button>
      </div>
      <div class="tbl-wrap"><div id="ordersList"></div></div>`;
    const load = async () => {
      const st = $('#ordStatus').value, ty = $('#ordType').value, q = $('#ordSearch').value.trim();
      const url = `/api/orders?${st ? `status=${st}&` : ''}${ty ? `type=${ty}&` : ''}${q ? `search=${encodeURIComponent(q)}` : ''}`;
      const list = await API.get(url);
      $('#ordersList').innerHTML = list.length ? list.map(o => `
        <div class="order-row" onclick="Admin.openOrderView(${o.id})">
          <div><div class="o-num">${esc(o.orderNumber)}</div><div class="o-sub">${fmt.dt(o.createdAt)}</div></div>
          <div class="o-info"><b>${o.typeName}${o.tableNumber ? ' · T' + o.tableNumber : ''} ${o.customerName ? '· ' + esc(o.customerName) : ''}</b>
            <div class="o-sub">${o.items.length} items ${o.createdByName ? '· ' + esc(o.createdByName) : ''} ${o.couponCode ? '· 🏷 ' + esc(o.couponCode) : ''}</div></div>
          ${badge(o.statusName)}${badge(o.paymentStatusName)}
          <div class="o-total">${money(o.total)}</div>
        </div>`).join('') : '<div class="empty"><div class="e-ico">🧾</div><b>No orders found</b></div>';
    };
    ['ordStatus', 'ordType'].forEach(id => $('#' + id).onchange = load);
    $('#ordSearch').addEventListener('keydown', e => { if (e.key === 'Enter') load(); });
    await load();
  },

  // ================= KITCHEN (admin view) =================
  async render_kitchen() {
    $('#main').innerHTML = this.head('Kitchen', 'Live tickets — advance each line as it fires.') + `
      <div class="filter-bar"><span class="muted">Auto-refreshes every 8s.</span></div>
      <div class="kds-grid" id="kdsGrid"></div>`;
    const load = async () => {
      const q = await API.get('/api/kitchen/queue');
      const tickets = [...q.active, ...q.ready];
      const grid = $('#kdsGrid');
      if (!grid) return;
      grid.innerHTML = tickets.length ? tickets.map(t => {
        const late = t.minutesElapsed > 20, warn = t.minutesElapsed > 12 && !late;
        return `
        <div class="ticket ${late ? 'late' : warn ? 'warn' : ''}">
          <div class="tk-head">
            <div><div class="tk-num">${esc(t.orderNumber)}</div><div class="tk-meta">${t.typeName} ${t.tableNumber ? '· T' + t.tableNumber : ''} · ${fmt.time(t.createdAt)}</div></div>
            <div class="tk-timer ${late ? 'late' : warn ? 'warn' : ''}">${t.minutesElapsed}m</div>
          </div>
          <div class="tk-lines">${t.lines.map(l => `
            <div class="tk-line"><div><span class="qty">${l.quantity}×</span>${esc(l.itemName)}
              ${l.modifierText ? `<span class="mod">${esc(l.modifierText)}</span>` : ''}${l.notes ? `<span class="note">${esc(l.notes)}</span>` : ''}</div>
              ${badge(l.statusName)}</div>`).join('')}
          </div>
          <div class="tk-foot">
            ${t.lines.some(l => l.statusName === 'Pending') ? `<button class="btn sm primary" onclick="Admin.bumpLines(${t.orderId},1,2)">Start All</button>` : ''}
            ${t.lines.some(l => l.statusName === 'Preparing') ? `<button class="btn sm success" onclick="Admin.bumpLines(${t.orderId},2,3)">Mark Ready</button>` : ''}
          </div>
        </div>`;
      }).join('') : '<div class="empty" style="grid-column:1/-1"><div class="e-ico">✓</div><b>All caught up!</b>No active tickets.</div>';
    };
    await load();
    this.pollTimer = setInterval(load, 8000);
  },

  async bumpLines(orderId, fromStatus, toStatus) {
    const q = await API.get('/api/kitchen/queue');
    const ticket = [...q.active, ...q.ready].find(t => t.orderId === orderId);
    if (!ticket) return;
    for (const line of ticket.lines.filter(l => l.status === fromStatus)) {
      await API.post(`/api/orders/items/${line.orderItemId}/status`, { status: toStatus });
    }
    this.render_kitchen();
  },
};

// ---------------- modal helpers (shared) ----------------
function openModal(html, size = '') {
  $('#modalCard').className = `modal-card ${size}`;
  $('#modalCard').innerHTML = html;
  $('#modal').classList.add('show');
}
function closeModal() { $('#modal').classList.remove('show'); }
window.openModal = openModal; window.closeModal = closeModal;
$('#modal')?.addEventListener('click', e => { if (e.target.id === 'modal') closeModal(); });

function fillDemo(email, pass) {
  $('#loginEmail').value = email;
  $('#loginPassword').value = pass;
}
window.fillDemo = fillDemo;

function AdminBoot() { Admin.boot(); }
window.AdminBoot = AdminBoot;
window.Admin = Admin;
