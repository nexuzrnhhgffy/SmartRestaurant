// ============================================================
//  Aurora Admin — Part 2: menu, reservations, events, inventory,
//  staff, accounting, customers, reviews, coupons, settings
// ============================================================
'use strict';

Object.assign(Admin, {

  // ================= MENU MANAGER =================
  async render_menu() {
    const [cats, items] = await Promise.all([
      API.get('/api/menu/categories?includeInactive=true'),
      API.get('/api/menu?includeUnavailable=true')
    ]);
    this._cats = cats; this._menuItems = items;
    $('#main').innerHTML = this.head('Menu Manager', `${items.length} dishes across ${cats.length} categories.`,
      `<button class="btn outline" onclick="Admin.saveCategoryModal()">+ Category</button>
       <button class="btn primary" onclick="Admin.saveItemModal()">+ Add Dish</button>`) + `
      <div class="filter-bar">${cats.map(c => `<span class="badge b-gray">${esc(c.name)} (${items.filter(i => i.categoryId === c.id).length})</span>`).join('')}</div>
      <div class="menu-mgr-grid">${items.map(m => `
        <div class="mi-card ${m.isAvailable ? '' : 'unavailable'}">
          <div class="mi-img">${esc(m.name.split(' ').map(w => w[0]).slice(0, 2).join(''))}</div>
          <div class="mi-body">
            <div class="mi-name">${esc(m.name)}</div>
            <div class="mi-cat">${esc(m.categoryName)} · station ${m.station}</div>
            <div class="muted" style="font-size:.76rem;margin:6px 0">cost ${money(m.price > 0 ? m.cost : 0)} · margin ${m.price > 0 ? Math.round((1 - m.cost / m.price) * 100) : 0}% · ordered ${m.timesOrdered}×</div>
            <div class="mi-foot">
              <span class="mi-price">${money(m.price)}</span>
              <span class="row-actions">
                <button class="btn xs ${m.isAvailable ? 'success' : 'outline'}" onclick="Admin.toggleAvailability(${m.id},${!m.isAvailable})">${m.isAvailable ? 'Live' : 'Sold out'}</button>
                <button class="btn xs outline" onclick="Admin.saveItemModal(${m.id})">Edit</button>
                <button class="btn xs danger" onclick="Admin.deleteItem(${m.id})">×</button>
              </span>
            </div>
          </div>
        </div>`).join('')}</div>`;
  },

  async toggleAvailability(id, available) {
    await API.post(`/api/menu/${id}/availability`, { id, isAvailable: available });
    toast(available ? 'Dish is live' : 'Marked sold out', '', 'success');
    this.render_menu();
  },

  async deleteItem(id) {
    if (!confirm('Delete this dish permanently?')) return;
    try { await API.del(`/api/menu/${id}`); toast('Deleted', '', 'success'); this.render_menu(); }
    catch (e) { toast('Failed', e.message, 'error'); }
  },

  saveItemModal(id) {
    const m = this._menuItems?.find(x => x.id === id) || {};
    openModal(`
      <div class="modal-head"><h3>${id ? 'Edit dish' : 'New dish'}</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="form-grid">
        <div class="field full"><label>Name *</label><input id="f_name" value="${esc(m.name || '')}"></div>
        <div class="field"><label>Category</label><select id="f_cat">${this._cats.map(c => `<option value="${c.id}" ${m.categoryId === c.id ? 'selected' : ''}>${esc(c.name)}</option>`).join('')}</select></div>
        <div class="field"><label>Station</label><select id="f_station">${[1,2,3,4,5,6].map(s => `<option value="${s}" ${m.station === s ? 'selected' : ''}">${['Bar','Cold','Grill','Fry','Pastry','HotLine'][s-1]}</option>`).join('')}</select></div>
        <div class="field"><label>Price *</label><input id="f_price" type="number" step="0.01" value="${m.price ?? ''}"></div>
        <div class="field"><label>Food cost</label><input id="f_cost" type="number" step="0.01" value="${m.cost ?? ''}"></div>
        <div class="field"><label>Prep time (min)</label><input id="f_prep" type="number" value="${m.prepTimeMinutes ?? 15}"></div>
        <div class="field"><label>Calories</label><input id="f_cal" type="number" value="${m.calories ?? ''}"></div>
        <div class="field full"><label>Description</label><textarea id="f_desc">${esc(m.description || '')}</textarea></div>
        <div class="field full"><label>Allergens (comma separated)</label><input id="f_allerg" value="${esc(m.allergens || '')}" placeholder="nuts, dairy"></div>
        <div class="switch-row"><input type="checkbox" id="f_avail" ${m.isAvailable !== false ? 'checked' : ''}><label for="f_avail" style="text-transform:none;letter-spacing:0">Available</label>
          <input type="checkbox" id="f_feat" ${m.isFeatured ? 'checked' : ''}><label for="f_feat" style="text-transform:none;letter-spacing:0">Chef's pick</label></div>
        <div class="switch-row"><input type="checkbox" id="f_veg" ${m.isVegetarian ? 'checked' : ''}><label for="f_veg" style="text-transform:none;letter-spacing:0">Vegetarian</label>
          <input type="checkbox" id="f_spicy" ${m.isSpicy ? 'checked' : ''}><label for="f_spicy" style="text-transform:none;letter-spacing:0">Spicy</label>
          <input type="checkbox" id="f_gf" ${m.isGlutenFree ? 'checked' : ''}><label for="f_gf" style="text-transform:none;letter-spacing:0">Gluten free</label></div>
      </div>
      <div class="modal-actions">
        <button class="btn outline" onclick="closeModal()">Cancel</button>
        <button class="btn primary" onclick="Admin.saveItem(${id || 0})">Save dish</button>
      </div>`);
  },

  async saveItem(id) {
    const dto = {
      id: id || null,
      categoryId: +$('#f_cat').value, name: $('#f_name').value.trim(),
      description: $('#f_desc').value.trim() || null,
      price: +$('#f_price').value || 0, cost: +$('#f_cost').value || 0,
      imageurl: null, isAvailable: $('#f_avail').checked, isFeatured: $('#f_feat').checked,
      isVegetarian: $('#f_veg').checked, isSpicy: $('#f_spicy').checked, isGlutenFree: $('#f_gf').checked,
      prepTimeMinutes: +$('#f_prep').value || 15, calories: +$('#f_cal').value || null,
      allergens: $('#f_allerg').value.trim() || null, station: +$('#f_station').value
    };
    if (!dto.name || !dto.price) return toast('Name & price required', '', 'error');
    await API.post('/api/menu', dto);
    closeModal(); toast('Dish saved', dto.name, 'success'); this.render_menu();
  },

  saveCategoryModal() {
    openModal(`
      <div class="modal-head"><h3>New category</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="field"><label>Name *</label><input id="c_name"></div>
      <div class="field"><label>Sort order</label><input id="c_sort" type="number" value="${(this._cats?.length || 0) + 1}"></div>
      <div class="field"><label>Description</label><input id="c_desc"></div>
      <div class="modal-actions">
        <button class="btn outline" onclick="closeModal()">Cancel</button>
        <button class="btn primary" onclick="Admin.saveCategory()">Create</button>
      </div>`);
  },

  async saveCategory() {
    await API.post('/api/menu/categories', { name: $('#c_name').value.trim(), sortorder: +$('#c_sort').value || 99, description: $('#c_desc').value.trim() || null, isactive: true });
    closeModal(); toast('Category created', '', 'success'); this.render_menu();
  },

  // ================= RESERVATIONS =================
  async render_reservations() {
    const today = new Date().toISOString().split('T')[0];
    $('#main').innerHTML = this.head('Reservations', 'Requests from the website, phone and front desk.',
      `<input type="date" id="resDate" value="${today}" style="padding:9px 13px;border:1px solid #e5e9f2;border-radius:10px">
       <button class="btn soft" onclick="Admin.render_reservations()">Filter</button>`) + `
      <div class="tbl-wrap"><table class="data"><thead>
        <tr><th>Guest</th><th>Party</th><th>When</th><th>Table</th><th>Occasion</th><th>Source</th><th>Status</th><th class="num">Actions</th></tr></thead>
        <tbody id="resBody"></tbody></table></div>`;
    $('#resDate').onchange = () => this.render_reservations();
    const date = $('#resDate').value;
    const list = await API.get(`/api/reservations?date=${date}`);
    const nextStatus = { Pending: 2, Confirmed: 3 };
    $('#resBody').innerHTML = list.length ? list.map(r => `
      <tr>
        <td><b>${esc(r.customerName)}</b><div class="muted" style="font-size:.78rem">${esc(r.phone)}</div></td>
        <td>${r.partySize} guests</td>
        <td>${fmt.dt(r.dateTime)}</td>
        <td>${r.tableId ? 'T' + r.tableId : '—'}</td>
        <td>${esc(r.occasion || '—')}</td>
        <td><span class="badge b-gray">${esc(r.source || '—')}</span></td>
        <td>${badge(r.statusName)}</td>
        <td><div class="row-actions">
          ${nextStatus[r.statusName] ? `<button class="btn xs primary" onclick="Admin.resStatus(${r.id},${nextStatus[r.statusName]})">${r.statusName === 'Pending' ? 'Confirm' : 'Seat'}</button>` : ''}
          ${!['Cancelled', 'NoShow', 'Seated'].includes(r.statusName) ? `<button class="btn xs danger" onclick="Admin.resStatus(${r.id},4)">Cancel</button>` : ''}
        </div></td>
      </tr>`).join('') : '<tr><td colspan="8"><div class="empty"><div class="e-ico">📅</div><b>No reservations for this day</b></div></td></tr>';
  },

  async resStatus(id, status) {
    await API.post(`/api/reservations/${id}/status`, { status });
    toast('Reservation updated', '', 'success'); this.render_reservations();
  },

  // ================= EVENTS =================
  async render_events() {
    const [bookings, packages] = await Promise.all([
      API.get('/api/events/bookings'), API.get('/api/events/packages')
    ]);
    const pipeline = ['Inquiry', 'Quoted', 'Confirmed', 'InProgress', 'Completed'];
    $('#main').innerHTML = this.head('Events & Catering', 'The events branch — weddings, corporate, birthdays.',
      `<button class="btn primary" onclick="Admin.bookingModal()">+ New Booking</button>`) + `
      <div class="grid-3 mb">
        ${packages.map(p => `
        <div class="card"><div class="card-title"><h3>${esc(p.name)}</h3><span class="badge b-teal">${money(p.pricePerPerson)}/pax</span></div>
          <p class="muted" style="font-size:.85rem">${esc(p.description || '')}</p>
          <p class="muted" style="font-size:.78rem;margin-top:8px">Min ${p.minGuests} guests · ${esc(p.includedServices || '')}</p></div>`).join('')}
      </div>
      <div class="tbl-wrap"><table class="data"><thead>
        <tr><th>Client</th><th>Event</th><th>Date</th><th>Guests</th><th>Quote</th><th>Deposit</th><th>Status</th><th class="num">Actions</th></tr></thead>
        <tbody>${bookings.map(b => `
          <tr>
            <td><b>${esc(b.customerName)}</b><div class="muted" style="font-size:.78rem">${esc(b.phone)}</div></td>
            <td><span class="badge b-pink">${esc(b.typeName)}</span>${b.packageName ? `<div class="muted" style="font-size:.74rem">${esc(b.packageName)}</div>` : ''}</td>
            <td>${fmt.d(b.eventDate)}</td>
            <td>${b.guestCount}</td>
            <td class="num"><b>${money(b.quoteAmount)}</b></td>
            <td>${b.depositPaid ? '<span class="badge b-green">Paid</span>' : `<span class="badge ${b.depositAmount ? 'b-amber' : 'b-gray'}">${money(b.depositAmount)}</span>`}</td>
            <td>${badge(b.statusName)}</td>
            <td><div class="row-actions">
              <button class="btn xs outline" onclick="Admin.bookingDetail(${b.id})">Open</button>
              ${b.statusName !== 'Confirmed' && pipeline.indexOf(b.statusName) < 3 ? `<button class="btn xs primary" onclick="Admin.evStatus(${b.id},${pipeline.indexOf(b.statusName) + 2})">${pipeline[pipeline.indexOf(b.statusName) + 1] || 'Advance'}</button>` : ''}
              ${!b.depositPaid && b.quoteAmount ? `<button class="btn xs success" onclick="Admin.evDeposit(${b.id})">Deposit</button>` : ''}
            </div></td>
          </tr>`).join('') || '<tr><td colspan="8"><div class="empty"><div class="e-ico">🎉</div><b>No event bookings yet</b></div></td></tr>'}
      </tbody></table></div>`;
  },

  async evStatus(id, status) { await API.post(`/api/events/bookings/${id}/status`, { status }); toast('Booking updated', '', 'success'); this.render_events(); },
  async evDeposit(id) {
    const b = (await API.get('/api/events/bookings')).find(x => x.id === id);
    openModal(`
      <div class="modal-head"><h3>Deposit — ${esc(b.customerName)}</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="field"><label>Deposit amount</label><input type="number" step="0.01" id="depAmt" value="${(b.quoteAmount * 0.15).toFixed(2)}"></div>
      <div class="modal-actions"><button class="btn outline" onclick="closeModal()">Cancel</button>
        <button class="btn success" onclick="Admin.evDepositSave(${id})">Mark Deposit Paid</button></div>`);
  },
  async evDepositSave(id) {
    await API.post(`/api/events/bookings/${id}/deposit?amount=${+$('#depAmt').value}&paid=true`, {});
    closeModal(); toast('Deposit recorded', 'Booking confirmed', 'success'); this.render_events();
  },

  async bookingDetail(id) {
    const b = (await API.get('/api/events/bookings')).find(x => x.id === id);
    const staff = await API.get('/api/users/staff');
    const pipeline = ['Inquiry', 'Quoted', 'Confirmed', 'InProgress', 'Completed'];
    openModal(`
      <div class="modal-head"><h3>${esc(b.customerName)} — ${esc(b.typeName)}</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="pipeline mb">${pipeline.map(p => `<div class="pipe-step ${p === b.statusName ? 'current' : pipeline.indexOf(p) < pipeline.indexOf(b.statusName) ? 'done' : ''}">${p}</div>`).join('')}</div>
      <div class="grid-2 mb" style="gap:8px">
        <div><span class="muted" style="font-size:.78rem">DATE</span><div><b>${fmt.d(b.eventDate)}</b></div></div>
        <div><span class="muted" style="font-size:.78rem">VENUE</span><div>${esc(b.venueName)}${b.address ? ' — ' + esc(b.address) : ''}</div></div>
        <div><span class="muted" style="font-size:.78rem">GUESTS</span><div><b>${b.guestCount}</b></div></div>
        <div><span class="muted" style="font-size:.78rem">QUOTE</span><div><b>${money(b.quoteAmount)}</b> ${b.depositPaid ? '· deposit paid' : ''}</div></div>
      </div>
      ${b.menuNotes ? `<div class="muted mb" style="font-size:.85rem"><b>Menu notes:</b> ${esc(b.menuNotes)}</div>` : ''}
      <div class="card-title"><h3>Checklist</h3><span class="muted" style="font-size:.8rem">${b.tasks.filter(t => t.isDone).length}/${b.tasks.length} done</span></div>
      <div class="mb">${b.tasks.map(t => `
        <div class="task-check ${t.isDone ? 'done' : ''}">
          <input type="checkbox" ${t.isDone ? 'checked' : ''} onchange="Admin.toggleTask(${t.id})">
          <label style="flex:1">${esc(t.title)}${t.assignedToName ? ` <span class="muted" style="font-size:.76rem">· ${esc(t.assignedToName)}</span>` : ''}</label>
          <span class="muted" style="font-size:.76rem">${t.dueDate ? fmt.d(t.dueDate) : ''}</span>
        </div>`).join('') || '<p class="muted">No tasks yet.</p>'}
      </div>
      <div class="flex wrap mb">
        <select id="taskAssign" style="padding:8px;border:1px solid #e5e9f2;border-radius:10px">${staff.map(s => `<option value="${s.id}">${esc(s.fullName)}</option>`).join('')}</select>
        <input id="taskTitle" placeholder="New task…" style="flex:1;padding:8px;border:1px solid #e5e9f2;border-radius:10px;min-width:140px">
        <button class="btn sm primary" onclick="Admin.addTask(${id})">+ Add task</button>
      </div>
      <div class="flex wrap">${['Confirmed', 'InProgress', 'Completed'].map((s, i) => `
        <button class="btn sm ${s === b.statusName ? 'soft' : 'outline'}" onclick="Admin.evStatus(${id},${i + 3});closeModal()">Set ${s}</button>`).join('')}</div>`, 'wide');
  },

  async toggleTask(id) { await API.post(`/api/events/tasks/${id}/toggle`, {}); },
  async addTask(bookingId) {
    const title = $('#taskTitle').value.trim();
    if (!title) return;
    await API.post(`/api/events/bookings/${bookingId}/tasks`, { title, assignedToId: +$('#taskAssign').value || null });
    this.bookingDetail(bookingId);
  },

  bookingModal() {
    openModal(`
      <div class="modal-head"><h3>New event booking</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="form-grid">
        <div class="field"><label>Client name *</label><input id="eb_name"></div>
        <div class="field"><label>Phone *</label><input id="eb_phone"></div>
        <div class="field"><label>Type</label><select id="eb_type">${[1,2,3,4,5,6].map(t => `<option value="${t}">${['Wedding','Corporate','Birthday','Graduation','Engagement','Other'][t-1]}</option>`).join('')}</select></div>
        <div class="field"><label>Date *</label><input id="eb_date" type="date" value="${new Date().toISOString().split('T')[0]}"></div>
        <div class="field"><label>Guests *</label><input id="eb_guests" type="number" value="50"></div>
        <div class="field"><label>Venue</label><select id="eb_venue">${[1,2,3,4].map(v => `<option value="${v}">${['Our Hall','Client Location','Terrace','Garden'][v-1]}</option>`).join('')}</select></div>
        <div class="field full"><label>Notes / menu requests</label><textarea id="eb_notes"></textarea></div>
      </div>
      <div class="modal-actions"><button class="btn outline" onclick="closeModal()">Cancel</button>
        <button class="btn primary" onclick="Admin.saveBooking()">Create booking</button></div>`);
  },

  async saveBooking() {
    await API.post('/api/events/bookings', {
      customerName: $('#eb_name').value.trim(), phone: $('#eb_phone').value.trim(),
      type: +$('#eb_type').value, eventDate: $('#eb_date').value,
      guestCount: +$('#eb_guests').value || 10, venue: +$('#eb_venue').value,
      notes: $('#eb_notes').value.trim() || null
    });
    closeModal(); toast('Booking created', '', 'success'); this.render_events();
  },

  // ================= INVENTORY =================
  async render_inventory() {
    const [ingredients, suppliers, pos, movements] = await Promise.all([
      API.get('/api/inventory/ingredients'), API.get('/api/inventory/suppliers'),
      API.get('/api/inventory/purchase-orders'), API.get('/api/inventory/movements')
    ]);
    const stockValue = ingredients.reduce((s, i) => s + i.stockValue, 0);
    const low = ingredients.filter(i => i.lowStock);
    $('#main').innerHTML = this.head('Inventory', 'Ingredients, suppliers, purchase orders and stock movements.',
      `<button class="btn outline" onclick="Admin.supplierModal()">+ Supplier</button>
       <button class="btn outline" onclick="Admin.poModal()">+ Purchase Order</button>
       <button class="btn primary" onclick="Admin.ingredientModal()">+ Ingredient</button>`) + `
      <div class="kpi-grid">
        <div class="kpi"><div class="k-label">Stock Value</div><div class="k-value">${money(stockValue)}</div></div>
        <div class="kpi ${low.length ? 'amber' : ''}"><div class="k-label">Low Stock</div><div class="k-value">${low.length}</div></div>
        <div class="kpi"><div class="k-label">Ingredients</div><div class="k-value">${ingredients.length}</div></div>
        <div class="kpi"><div class="k-label">Open POs</div><div class="k-value">${pos.filter(p => p.statusName === 'Ordered').length}</div></div>
      </div>
      ${low.length ? `<div class="card mb lowstock" style="border-left:4px solid #ef4444"><b>Reorder now:</b> ${low.map(i => `${esc(i.name)} (${i.stockQty} ${i.unit} / min ${i.minStock})`).join(' · ')}</div>` : ''}
      <div class="tbl-wrap mb"><table class="data"><thead>
        <tr><th>Ingredient</th><th>Stock</th><th>Min</th><th>Unit cost</th><th>Value</th><th>Supplier</th><th>Storage</th><th class="num">Actions</th></tr></thead>
        <tbody>${ingredients.map(i => `
          <tr><td><b>${esc(i.name)}</b>${i.lowStock ? ' <span class="badge b-red">LOW</span>' : ''}</td>
            <td>${i.stockQty} ${i.unit}</td><td>${i.minStock} ${i.unit}</td>
            <td class="num">${money(i.costPerUnit)}</td><td class="num">${money(i.stockValue)}</td>
            <td>${esc(i.supplierName || '—')}</td><td><span class="badge b-gray">${i.storageName}</span></td>
            <td><div class="row-actions">
              <button class="btn xs outline" onclick="Admin.adjustModal(${i.id},'${esc(i.name)}')">Adjust</button>
              <button class="btn xs outline" onclick="Admin.ingredientModal(${i.id})">Edit</button>
            </div></td></tr>`).join('')}</tbody></table></div>
      <div class="grid-2">
        <div class="card"><div class="card-title"><h3>Purchase Orders</h3></div>
          ${pos.map(p => `<div class="order-row" style="grid-template-columns:1fr auto auto">
            <div><b class="o-num">${esc(p.poNumber)}</b><div class="o-sub">${esc(p.supplierName)} · ${p.items.length} lines · ${fmt.d(p.createdAt)}</div></div>
            <div class="o-total">${money(p.total)}</div>${badge(p.statusName)}
            ${p.statusName === 'Ordered' ? `<button class="btn xs success" onclick="Admin.receivePO(${p.id})">Receive</button>` : ''}
          </div>`).join('') || '<p class="muted">No purchase orders.</p>'}</div>
        <div class="card"><div class="card-title"><h3>Recent Movements</h3></div>
          ${movements.slice(0, 10).map(m => `
          <div class="flex" style="justify-content:space-between;padding:8px 0;border-bottom:1px dashed #eef1f6;font-size:.86rem">
            <div><b>${esc(m.ingredientName)}</b> <span class="muted" style="font-size:.74rem">${m.typeName}</span></div>
            <div><span style="color:${m.quantity >= 0 ? '#22c55e' : '#ef4444'};font-weight:800">${m.quantity >= 0 ? '+' : ''}${m.quantity}</span> <span class="muted" style="font-size:.72rem">→ ${m.stockAfter}</span></div>
          </div>`).join('')}</div>
      </div>`;
  },

  ingredientModal(id) {
    const ing = window._lastIngredients?.find(x => x.id === id) || {};
    (async () => {
      if (!window._suppliersCache) window._suppliersCache = await API.get('/api/inventory/suppliers');
      const sup = window._suppliersCache;
      if (id && !ing.id) { const list = await API.get('/api/inventory/ingredients'); Object.assign(ing, list.find(x => x.id === id) || {}); }
      openModal(`
        <div class="modal-head"><h3>${id ? 'Edit' : 'New'} ingredient</h3><button class="close-x" onclick="closeModal()">×</button></div>
        <div class="form-grid">
          <div class="field full"><label>Name *</label><input id="ing_name" value="${esc(ing.name || '')}"></div>
          <div class="field"><label>Unit</label><select id="ing_unit">${['kg', 'g', 'L', 'pcs'].map(u => `<option ${ing.unit === u ? 'selected' : ''}>${u}</option>`).join('')}</select></div>
          <div class="field"><label>Storage</label><select id="ing_storage">${[1, 2, 3].map(s => `<option value="${s}" ${ing.storage === s ? 'selected' : ''}>${['Dry', 'Fridge', 'Freezer'][s - 1]}</option>`).join('')}</select></div>
          <div class="field"><label>Stock qty</label><input id="ing_stock" type="number" step="0.001" value="${ing.stockQty ?? 0}"></div>
          <div class="field"><label>Min stock</label><input id="ing_min" type="number" step="0.001" value="${ing.minStock ?? 0}"></div>
          <div class="field"><label>Cost / unit</label><input id="ing_cost" type="number" step="0.0001" value="${ing.costPerUnit ?? 0}"></div>
          <div class="field"><label>Supplier</label><select id="ing_sup"><option value="">—</option>${sup.map(s => `<option value="${s.id}" ${ing.supplierId === s.id ? 'selected' : ''}>${esc(s.name)}</option>`).join('')}</select></div>
        </div>
        <div class="modal-actions"><button class="btn outline" onclick="closeModal()">Cancel</button>
          <button class="btn primary" onclick="Admin.saveIngredient(${id || 0})">Save</button></div>`);
    })();
  },

  async saveIngredient(id) {
    await API.post('/api/inventory/ingredients', {
      id: id || null, name: $('#ing_name').value.trim(), unit: $('#ing_unit').value,
      stockQty: +$('#ing_stock').value || 0, minStock: +$('#ing_min').value || 0,
      costPerUnit: +$('#ing_cost').value || 0, storage: +$('#ing_storage').value,
      supplierId: +$('#ing_sup').value || null
    });
    closeModal(); toast('Ingredient saved', '', 'success'); this.render_inventory();
  },

  adjustModal(id, name) {
    openModal(`
      <div class="modal-head"><h3>Adjust stock — ${esc(name)}</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="field"><label>Type</label><select id="adj_type"><option value="2">Usage (−)</option><option value="3">Waste (−)</option><option value="4">Adjustment (±)</option><option value="5">Return (−)</option></select></div>
      <div class="field"><label>Quantity (use negative for adjustment down)</label><input id="adj_qty" type="number" step="0.001" value="0"></div>
      <div class="field"><label>Reason</label><input id="adj_reason" placeholder="Spoilage, inventory count..."></div>
      <div class="modal-actions"><button class="btn outline" onclick="closeModal()">Cancel</button>
        <button class="btn primary" onclick="Admin.saveAdjust(${id})">Apply</button></div>`);
  },

  async saveAdjust(id) {
    await API.post('/api/inventory/adjust', { ingredientId: id, quantity: +$('#adj_qty').value, type: +$('#adj_type').value, reason: $('#adj_reason').value.trim() || null });
    closeModal(); toast('Stock adjusted', '', 'success'); this.render_inventory();
  },

  supplierModal() {
    openModal(`
      <div class="modal-head"><h3>New supplier</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="form-grid">
        <div class="field"><label>Name *</label><input id="sup_name"></div>
        <div class="field"><label>Contact</label><input id="sup_contact"></div>
        <div class="field"><label>Phone *</label><input id="sup_phone"></div>
        <div class="field"><label>Email</label><input id="sup_email"></div>
      </div>
      <div class="modal-actions"><button class="btn outline" onclick="closeModal()">Cancel</button>
        <button class="btn primary" onclick="Admin.saveSupplier()">Save</button></div>`);
  },

  async saveSupplier() {
    await API.post('/api/inventory/suppliers', { name: $('#sup_name').value.trim(), contactName: $('#sup_contact').value.trim() || null, phone: $('#sup_phone').value.trim(), email: $('#sup_email').value.trim() || null, rating: 4, isActive: true });
    closeModal(); toast('Supplier saved', '', 'success'); this.render_inventory();
  },

  async poModal() {
    if (!window._ingCache) window._ingCache = await API.get('/api/inventory/ingredients');
    if (!window._suppliersCache) window._suppliersCache = await API.get('/api/inventory/suppliers');
    openModal(`
      <div class="modal-head"><h3>New purchase order</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="field"><label>Supplier</label><select id="po_sup">${window._suppliersCache.map(s => `<option value="${s.id}">${esc(s.name)}</option>`).join('')}</select></div>
      <div class="field"><label>Expected date</label><input id="po_date" type="date" value="${new Date(Date.now() + 6048e5).toISOString().split('T')[0]}"></div>
      <div id="poLines">
        <div class="flex mb"><select class="po-ing" style="flex:1;padding:8px;border:1px solid #e5e9f2;border-radius:10px">${window._ingCache.map(i => `<option value="${i.id}">${esc(i.name)} (${i.unit})</option>`).join('')}</select>
          <input class="po-qty" type="number" step="0.001" placeholder="Qty" style="width:90px;padding:8px;border:1px solid #e5e9f2;border-radius:10px">
          <input class="po-cost" type="number" step="0.0001" placeholder="Cost" style="width:100px;padding:8px;border:1px solid #e5e9f2;border-radius:10px">
          <button class="btn xs danger" onclick="this.parentElement.remove()">×</button></div>
      </div>
      <button class="btn sm outline" onclick="Admin.addPoLine()">+ Line</button>
      <div class="modal-actions"><button class="btn outline" onclick="closeModal()">Cancel</button>
        <button class="btn primary" onclick="Admin.savePO()">Create PO</button></div>`);
  },

  addPoLine() {
    $('#poLines').insertAdjacentHTML('beforeend', `
      <div class="flex mb"><select class="po-ing" style="flex:1;padding:8px;border:1px solid #e5e9f2;border-radius:10px">${window._ingCache.map(i => `<option value="${i.id}">${esc(i.name)} (${i.unit})</option>`).join('')}</select>
        <input class="po-qty" type="number" step="0.001" placeholder="Qty" style="width:90px;padding:8px;border:1px solid #e5e9f2;border-radius:10px">
        <input class="po-cost" type="number" step="0.0001" placeholder="Cost" style="width:100px;padding:8px;border:1px solid #e5e9f2;border-radius:10px">
        <button class="btn xs danger" onclick="this.parentElement.remove()">×</button></div>`);
  },

  async savePO() {
    const items = [...document.querySelectorAll('#poLines .flex')].map(row => ({
      ingredientId: +row.querySelector('.po-ing').value,
      quantity: +row.querySelector('.po-qty').value || 0,
      unitCost: +row.querySelector('.po-cost').value || 0
    })).filter(i => i.quantity > 0);
    if (!items.length) return toast('Add at least one line', '', 'error');
    await API.post('/api/inventory/purchase-orders', { supplierId: +$('#po_sup').value, expectedDate: $('#po_date').value, items });
    closeModal(); toast('Purchase order created', '', 'success'); this.render_inventory();
  },

  async receivePO(id) {
    await API.post(`/api/inventory/purchase-orders/${id}/receive`, {});
    toast('PO received', 'Stock updated', 'success'); this.render_inventory();
  },

  // ================= STAFF =================
  async render_staff() {
    const [staff, shifts, attendance] = await Promise.all([
      API.get('/api/users/staff'), API.get('/api/staff/shifts'), API.get('/api/staff/attendance')
    ]);
    $('#main').innerHTML = this.head('Staff & Shifts', 'Team directory, scheduling and attendance.',
      `<button class="btn primary" onclick="Admin.staffModal()">+ Add Staff</button>`) + `
      <div class="grid-2 mb">
        <div class="tbl-wrap"><table class="data"><thead><tr><th>Member</th><th>Role</th><th>Salary</th><th>Active</th><th class="num">Actions</th></tr></thead>
          <tbody>${staff.map(s => `
            <tr><td><div class="flex"><div class="avatar" style="width:32px;height:32px;border-radius:50%;display:grid;place-items:center;color:#fff;font-weight:700;font-size:.75rem;background:${avatarColor(s.fullName)}">${initials(s.fullName)}</div>
              <div><b>${esc(s.fullName)}</b><div class="muted" style="font-size:.76rem">${esc(s.email)}</div></div></div></td>
              <td><span class="badge ${s.role === 1 ? 'b-indigo' : 'b-gray'}">${ROLE_NAMES[s.role]}</span>${s.jobTitle ? `<div class="muted" style="font-size:.74rem">${esc(s.jobTitle)}</div>` : ''}</td>
              <td class="num">${s.baseSalary ? money(s.baseSalary) : '—'}</td>
              <td>${s.isActive ? '<span class="badge b-green">Active</span>' : '<span class="badge b-red">Off</span>'}</td>
              <td><div class="row-actions">
                <button class="btn xs outline" onclick="Admin.staffModal(${s.id})">Edit</button>
                ${s.role !== 1 ? `<button class="btn xs ${s.isActive ? 'danger' : 'success'}" onclick="Admin.toggleStaff(${s.id})">${s.isActive ? 'Deactivate' : 'Activate'}</button>` : ''}
              </div></td></tr>`).join('')}</tbody></table></div>
        <div class="card"><div class="card-title"><h3>Shifts</h3><button class="btn xs primary" onclick="Admin.shiftModal()">+ Shift</button></div>
          ${shifts.slice(0, 8).map(sh => `
            <div class="flex" style="justify-content:space-between;padding:9px 0;border-bottom:1px dashed #eef1f6">
              <div><b style="font-size:.88rem">${esc(sh.employeeName)}</b><div class="muted" style="font-size:.76rem">${fmt.dt(sh.startTime)} → ${fmt.time(sh.endTime)}</div></div>
              ${badge(sh.statusName)}
            </div>`).join('')}
        </div>
      </div>
      <div class="card"><div class="card-title"><h3>Attendance — recent</h3></div>
        <div class="tbl-wrap" style="border:none;box-shadow:none"><table class="data"><thead><tr><th>Employee</th><th>Check-in</th><th>Check-out</th><th class="num">Hours</th></tr></thead>
          <tbody>${attendance.slice(0, 10).map(a => `
            <tr><td><b>${esc(a.employeeName)}</b></td><td>${fmt.dt(a.checkIn)}</td><td>${a.checkOut ? fmt.dt(a.checkOut) : '<span class="badge b-amber">On shift</span>'}</td>
            <td class="num">${a.hoursWorked ? a.hoursWorked.toFixed(1) + 'h' : '—'}</td></tr>`).join('')}</tbody></table></div>
      </div>`;
  },

  staffModal(id) {
    (async () => {
      let s = {};
      if (id) s = (await API.get('/api/users/staff')).find(x => x.id === id) || {};
      openModal(`
        <div class="modal-head"><h3>${id ? 'Edit' : 'New'} staff member</h3><button class="close-x" onclick="closeModal()">×</button></div>
        <div class="form-grid">
          <div class="field"><label>Full name *</label><input id="st_name" value="${esc(s.fullName || '')}"></div>
          <div class="field"><label>Email *</label><input id="st_email" type="email" value="${esc(s.email || '')}"></div>
          <div class="field"><label>Phone</label><input id="st_phone" value="${esc(s.phone || '')}"></div>
          <div class="field"><label>Role</label><select id="st_role">${[1,2,3,4,5,6].map(r => `<option value="${r}" ${s.role === r ? 'selected' : ''}>${ROLE_NAMES[r]}</option>`).join('')}</select></div>
          <div class="field"><label>Job title</label><input id="st_title" value="${esc(s.jobTitle || '')}"></div>
          <div class="field"><label>Base salary</label><input id="st_salary" type="number" step="0.01" value="${s.baseSalary ?? ''}"></div>
          <div class="field full"><label>${id ? 'New password (leave blank to keep)' : 'Password'}</label><input id="st_pass" type="password" placeholder="${id ? 'Unchanged' : 'Welcome@123 default'}"></div>
        </div>
        <div class="modal-actions"><button class="btn outline" onclick="closeModal()">Cancel</button>
          <button class="btn primary" onclick="Admin.saveStaff(${id || 0})">Save</button></div>`);
    })();
  },

  async saveStaff(id) {
    const dto = {
      id: id || 0, fullName: $('#st_name').value.trim(), email: $('#st_email').value.trim(),
      phone: $('#st_phone').value.trim(), role: +$('#st_role').value, isActive: true,
      jobTitle: $('#st_title').value.trim() || null, baseSalary: +$('#st_salary').value || null
    };
    if (!dto.fullName || !dto.email) return toast('Name & email required', '', 'error');
    await API.post(`/api/users/staff${id ? `?password=${encodeURIComponent($('#st_pass').value)}` : `?password=${encodeURIComponent($('#st_pass').value || 'Welcome@123')}`}`, dto);
    closeModal(); toast('Staff saved', dto.fullName, 'success'); this.render_staff();
  },

  async toggleStaff(id) { await API.post(`/api/users/staff/${id}/toggle`, {}); toast('Updated', '', 'success'); this.render_staff(); },

  shiftModal() {
    (async () => {
      const staff = await API.get('/api/users/staff');
      const d = n => new Date(Date.now() + n * 864e5).toISOString().split('T')[0];
      openModal(`
        <div class="modal-head"><h3>New shift</h3><button class="close-x" onclick="closeModal()">×</button></div>
        <div class="field"><label>Employee</label><select id="sh_emp">${staff.map(s => `<option value="${s.id}">${esc(s.fullName)}</option>`).join('')}</select></div>
        <div class="form-grid">
          <div class="field"><label>Date</label><input id="sh_date" type="date" value="${d(1)}"></div>
          <div class="field"><label>Shift</label><select id="sh_hours"><option value="10,18">Morning 10–18</option><option value="16,24" selected>Evening 16–24</option><option value="14,23">Mid 14–23</option></select></div>
        </div>
        <div class="modal-actions"><button class="btn outline" onclick="closeModal()">Cancel</button>
          <button class="btn primary" onclick="Admin.saveShift()">Schedule</button></div>`);
    })();
  },

  async saveShift() {
    const [a, b] = $('#sh_hours').value.split(',').map(Number);
    const base = new Date($('#sh_date').value + 'T12:00:00Z');
    const start = new Date(base); start.setUTCHours(a);
    const end = new Date(base); end.setUTCHours(b);
    await API.post('/api/staff/shifts', { employeeId: +$('#sh_emp').value, startTime: start.toISOString(), endTime: end.toISOString() });
    closeModal(); toast('Shift scheduled', '', 'success'); this.render_staff();
  },

  // ================= ACCOUNTING =================
  async render_finance() {
    const monthStart = new Date(); monthStart.setDate(1);
    const [pl, expenses, payrolls] = await Promise.all([
      API.get(`/api/accounting/profit-loss?from=${new Date(Date.now() - 30 * 864e5).toISOString()}&to=${new Date().toISOString()}`),
      API.get(`/api/accounting/expenses?from=${monthStart.toISOString()}`),
      API.get('/api/accounting/payroll')
    ]);
    $('#main').innerHTML = this.head('Accounting', 'Revenue, expenses, payroll and profit & loss.') + `
      <div class="kpi-grid">
        <div class="kpi teal"><div class="k-label">Revenue (30d)</div><div class="k-value">${money(pl.revenue)}</div><div class="k-delta flat">net of tax</div></div>
        <div class="kpi"><div class="k-label">Food Cost</div><div class="k-value">${money(pl.foodCost)}</div><div class="k-delta flat">${pl.revenue ? Math.round(pl.foodCost / pl.revenue * 100) : 0}% of revenue</div></div>
        <div class="kpi amber"><div class="k-label">Expenses</div><div class="k-value">${money(pl.expenses)}</div><div class="k-delta flat">+ payroll ${money(pl.payroll)}</div></div>
        <div class="kpi ${pl.netProfit >= 0 ? '' : 'rose'}" style="${pl.netProfit >= 0 ? 'background:linear-gradient(135deg,#16a34a,#22c55e);border:none;color:#fff' : ''}"><div class="k-label">Net Profit</div><div class="k-value">${money(pl.netProfit)}</div><div class="k-delta flat">margin ${pl.profitMargin}%</div></div>
      </div>
      <div class="grid-21 mb">
        <div class="card"><div class="card-title"><h3>Revenue Trend</h3></div><div class="chart-box"><canvas id="chPL"></canvas></div></div>
        <div class="card"><div class="card-title"><h3>Expense Breakdown</h3></div><div class="chart-box"><canvas id="chExp"></canvas></div>${legendHtml(pl.expenseBreakdown.map(e => ({ label: e.category, value: e.amount })))}</div>
      </div>
      <div class="grid-2">
        <div class="card"><div class="card-title"><h3>Expenses — this month</h3>
          <button class="btn xs primary" onclick="Admin.expenseModal()">+ Expense</button></div>
          ${expenses.map(e => `
          <div class="flex" style="justify-content:space-between;padding:9px 0;border-bottom:1px dashed #eef1f6;font-size:.88rem">
            <div><b>${esc(e.description)}</b><div class="muted" style="font-size:.74rem">${esc(e.categoryName)} · ${fmt.d(e.date)}${e.paidTo ? ' · ' + esc(e.paidTo) : ''}</div></div>
            <b>${money(e.amount)}</b>
          </div>`).join('') || '<p class="muted">No expenses recorded.</p>'}</div>
        <div class="card"><div class="card-title"><h3>Payroll — current period</h3>
          <button class="btn xs primary" onclick="Admin.payrollModal()">+ Payroll</button></div>
          ${payrolls.map(p => `
          <div class="flex" style="justify-content:space-between;padding:9px 0;border-bottom:1px dashed #eef1f6;font-size:.88rem">
            <div><b>${esc(p.employeeName)}</b><div class="muted" style="font-size:.74rem">${p.period} · base ${money(p.baseSalary)}${p.overtimePay ? ' · OT ' + money(p.overtimePay) : ''}${p.bonus ? ' · bonus ' + money(p.bonus) : ''}</div></div>
            <div style="text-align:right"><b>${money(p.netPay)}</b><div>${p.statusName === 'Paid' ? '<span class="badge b-green">Paid</span>' : `<button class="btn xs success" onclick="Admin.payPayroll(${p.id})">Pay</button>`}</div></div>
          </div>`).join('')}</div>
      </div>`;
    Charts.line($('#chPL'), pl.revenueTrend, { currency: true, color: '#0ea5a4' });
    Charts.donut($('#chExp'), pl.expenseBreakdown.map(e => ({ label: e.category, value: e.amount })), { center: money(pl.expenses).replace('$', '$') });
  },

  expenseModal() {
    openModal(`
      <div class="modal-head"><h3>Record expense</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="form-grid">
        <div class="field"><label>Category</label><select id="ex_cat">${[1,2,3,4,5,6,7,8,9].map(c => `<option value="${c}">${['Rent','Utilities','Salaries','Supplies','Marketing','Maintenance','Taxes','Insurance','Misc'][c-1]}</option>`).join('')}</select></div>
        <div class="field"><label>Amount *</label><input id="ex_amt" type="number" step="0.01"></div>
        <div class="field"><label>Description *</label><input id="ex_desc"></div>
        <div class="field"><label>Date</label><input id="ex_date" type="date" value="${new Date().toISOString().split('T')[0]}"></div>
        <div class="field"><label>Paid to</label><input id="ex_paid"></div>
        <div class="field"><label>Receipt ref</label><input id="ex_ref"></div>
      </div>
      <div class="modal-actions"><button class="btn outline" onclick="closeModal()">Cancel</button>
        <button class="btn primary" onclick="Admin.saveExpense()">Record</button></div>`);
  },

  async saveExpense() {
    await API.post('/api/accounting/expenses', {
      category: +$('#ex_cat').value, amount: +$('#ex_amt').value,
      description: $('#ex_desc').value.trim(), date: $('#ex_date').value,
      paidTo: $('#ex_paid').value.trim() || null, receiptRef: $('#ex_ref').value.trim() || null
    });
    closeModal(); toast('Expense recorded', '', 'success'); this.render_finance();
  },

  payrollModal() {
    (async () => {
      const staff = await API.get('/api/users/staff');
      const period = new Date().toISOString().slice(0, 7);
      openModal(`
        <div class="modal-head"><h3>Generate payroll</h3><button class="close-x" onclick="closeModal()">×</button></div>
        <div class="field"><label>Employee</label><select id="pr_emp">${staff.map(s => `<option value="${s.id}">${esc(s.fullName)}</option>`).join('')}</select></div>
        <div class="form-grid">
          <div class="field"><label>Period</label><input id="pr_period" value="${period}"></div>
          <div class="field"><label>Overtime hours</label><input id="pr_ot" type="number" step="0.5" value="0"></div>
          <div class="field"><label>Bonus</label><input id="pr_bonus" type="number" step="0.01" value="0"></div>
          <div class="field"><label>Deductions</label><input id="pr_ded" type="number" step="0.01" value="0"></div>
        </div>
        <div class="modal-actions"><button class="btn outline" onclick="closeModal()">Cancel</button>
          <button class="btn primary" onclick="Admin.savePayroll()">Generate</button></div>`);
    })();
  },

  async savePayroll() {
    await API.post('/api/accounting/payroll', {
      employeeId: +$('#pr_emp').value, period: $('#pr_period').value,
      overtimeHours: +$('#pr_ot').value || 0, bonus: +$('#pr_bonus').value || 0, deductions: +$('#pr_ded').value || 0
    });
    closeModal(); toast('Payroll generated', '', 'success'); this.render_finance();
  },

  async payPayroll(id) { await API.post(`/api/accounting/payroll/${id}/pay`, {}); toast('Payroll paid', '', 'success'); this.render_finance(); },

  // ================= CUSTOMERS =================
  async render_customers() {
    const list = await API.get('/api/customers');
    $('#main').innerHTML = this.head('Customers & Loyalty', `${list.length} members — points, tiers and spend history.`) + `
      <div class="tbl-wrap"><table class="data"><thead>
        <tr><th>Customer</th><th>Tier</th><th class="num">Points</th><th class="num">Total spent</th><th class="num">Orders</th><th>Member since</th></tr></thead>
        <tbody>${list.map(c => `
          <tr><td><div class="flex"><div class="avatar" style="width:32px;height:32px;border-radius:50%;display:grid;place-items:center;color:#fff;font-weight:700;font-size:.72rem;background:${avatarColor(c.fullName)}">${initials(c.fullName)}</div>
            <div><b>${esc(c.fullName)}</b><div class="muted" style="font-size:.76rem">${esc(c.phone)}</div></div></div></td>
            <td><span class="badge ${c.tier === 'Platinum' ? 'b-indigo' : c.tier === 'Gold' ? 'b-amber' : c.tier === 'Silver' ? 'b-blue' : 'b-gray'}">${c.tier}</span></td>
            <td class="num"><b>${c.loyaltyPoints}</b></td>
            <td class="num">${money(c.totalSpent)}</td>
            <td class="num">${c.orderCount}</td>
            <td>${fmt.d(c.createdAt)}</td></tr>`).join('')}</tbody></table></div>`;
  },

  // ================= REVIEWS =================
  async render_reviews() {
    const list = await API.get('/api/reviews');
    $('#main').innerHTML = this.head('Reviews & Reputation', 'Moderate guest feedback and reply publicly.') + `
      <div class="grid-3">
        ${list.map(r => `
        <div class="card">
          <div class="flex" style="justify-content:space-between;margin-bottom:8px">
            <b style="color:#f59e0b;font-size:1.05rem">${'★'.repeat(r.rating)}${'☆'.repeat(5 - r.rating)}</b>${badge(r.statusName)}
          </div>
          <p style="font-size:.9rem;font-style:italic">"${esc(r.comment || '')}"</p>
          <div class="muted mt" style="font-size:.78rem">${esc(r.customerName)} · ${esc(r.typeName)} · ${fmt.d(r.createdAt)}</div>
          ${r.reply ? `<div class="mt" style="background:rgba(79,70,229,.06);padding:10px 14px;border-radius:10px;font-size:.82rem"><b>Reply:</b> ${esc(r.reply)}</div>` : ''}
          ${r.statusName === 'Pending' ? `<div class="flex mt"><button class="btn xs success" onclick="Admin.moderate(${r.id},2)">Approve</button>
            <button class="btn xs danger" onclick="Admin.moderate(${r.id},3)">Reject</button>
            <button class="btn xs outline" onclick="Admin.replyReview(${r.id})">Reply</button></div>` : ''}
        </div>`).join('') || '<div class="empty" style="grid-column:1/-1">No reviews yet.</div>'}
      </div>`;
  },

  async moderate(id, status) { await API.post(`/api/reviews/${id}/moderate`, { status }); toast('Review moderated', '', 'success'); this.render_reviews(); },
  replyReview(id) {
    openModal(`
      <div class="modal-head"><h3>Public reply</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="field"><textarea id="rv_reply" placeholder="Thank you for your feedback..."></textarea></div>
      <div class="modal-actions"><button class="btn outline" onclick="closeModal()">Cancel</button>
        <button class="btn primary" onclick="Admin.saveReply(${id})">Post Reply</button></div>`);
  },
  async saveReply(id) { await API.post(`/api/reviews/${id}/reply`, { reply: $('#rv_reply').value.trim() }); closeModal(); toast('Reply posted', '', 'success'); this.render_reviews(); },

  // ================= COUPONS =================
  async render_coupons() {
    const list = await API.get('/api/coupons');
    $('#main').innerHTML = this.head('Coupons & Promotions', 'Discount codes for online and dine-in orders.',
      `<button class="btn primary" onclick="Admin.couponModal()">+ New Coupon</button>`) + `
      <div class="tbl-wrap"><table class="data"><thead>
        <tr><th>Code</th><th>Discount</th><th>Min order</th><th class="num">Used</th><th>Valid until</th><th>Status</th><th class="num">Actions</th></tr></thead>
        <tbody>${list.map(c => `
          <tr><td><b class="mono">${esc(c.code)}</b><div class="muted" style="font-size:.76rem">${esc(c.description || '')}</div></td>
            <td>${c.type === 1 ? c.value + '%' : money(c.value)}</td>
            <td>${money(c.minOrderAmount)}</td>
            <td class="num">${c.usedCount}/${c.maxUses}</td>
            <td>${c.validTo ? fmt.d(c.validTo) : 'No expiry'}</td>
            <td>${c.isValid ? '<span class="badge b-green">Active</span>' : '<span class="badge b-red">Expired</span>'}</td>
            <td><div class="row-actions"><button class="btn xs danger" onclick="Admin.deleteCoupon(${c.id})">Delete</button></div></td></tr>`).join('')}</tbody></table></div>`;
  },

  couponModal() {
    openModal(`
      <div class="modal-head"><h3>New coupon</h3><button class="close-x" onclick="closeModal()">×</button></div>
      <div class="form-grid">
        <div class="field"><label>Code *</label><input id="cp_code" placeholder="SUMMER20" style="text-transform:uppercase"></div>
        <div class="field"><label>Type</label><select id="cp_type"><option value="1">Percent %</option><option value="2">Fixed $</option></select></div>
        <div class="field"><label>Value *</label><input id="cp_value" type="number" step="0.01"></div>
        <div class="field"><label>Min order</label><input id="cp_min" type="number" step="0.01" value="0"></div>
        <div class="field"><label>Max uses</label><input id="cp_max" type="number" value="100"></div>
        <div class="field"><label>Valid to</label><input id="cp_valid" type="date" value="${new Date(Date.now() + 90 * 864e5).toISOString().split('T')[0]}"></div>
        <div class="field full"><label>Description</label><input id="cp_desc"></div>
      </div>
      <div class="modal-actions"><button class="btn outline" onclick="closeModal()">Cancel</button>
        <button class="btn primary" onclick="Admin.saveCoupon()">Create</button></div>`);
  },

  async saveCoupon() {
    await API.post('/api/coupons', {
      code: $('#cp_code').value, type: +$('#cp_type').value, value: +$('#cp_value').value,
      minOrderAmount: +$('#cp_min').value || 0, maxUses: +$('#cp_max').value || 100,
      validFrom: null, validTo: $('#cp_valid').value || null,
      description: $('#cp_desc').value.trim() || null, isActive: true
    });
    closeModal(); toast('Coupon created', '', 'success'); this.render_coupons();
  },

  async deleteCoupon(id) { if (!confirm('Delete coupon?')) return; await API.del(`/api/coupons/${id}`); toast('Deleted', '', 'success'); this.render_coupons(); },

  // ================= SETTINGS =================
  async render_settings() {
    const s = await API.get('/api/settings');
    $('#main').innerHTML = this.head('Restaurant Settings', 'Branding, pricing rules and service toggles (SuperAdmin only).') + `
      <div class="grid-2">
        <div class="card"><div class="card-title"><h3>Identity</h3></div>
          <div class="field"><label>Name</label><input id="s_name" value="${esc(s.name)}"></div>
          <div class="field"><label>Tagline</label><input id="s_tag" value="${esc(s.tagline || '')}"></div>
          <div class="field"><label>Address</label><input id="s_addr" value="${esc(s.address || '')}"></div>
          <div class="grid-2" style="gap:0 12px">
            <div class="field"><label>Phone</label><input id="s_phone" value="${esc(s.phone || '')}"></div>
            <div class="field"><label>Email</label><input id="s_email" value="${esc(s.email || '')}"></div>
          </div>
        </div>
        <div class="card"><div class="card-title"><h3>Financial & Service</h3></div>
          <div class="grid-2" style="gap:0 12px">
            <div class="field"><label>Tax rate (0.09 = 9%)</label><input id="s_tax" type="number" step="0.001" value="${s.taxRate}"></div>
            <div class="field"><label>Service charge</label><input id="s_svc" type="number" step="0.001" value="${s.serviceChargeRate}"></div>
            <div class="field"><label>Delivery fee</label><input id="s_del" type="number" step="0.01" value="${s.deliveryFee}"></div>
            <div class="field"><label>Loyalty pts / $</label><input id="s_pts" type="number" value="${s.loyaltyPointsPerDollar}"></div>
            <div class="field"><label>Opening hours</label><input id="s_hours" value="${esc(s.openingHours || '')}"></div>
            <div class="field"><label>Delivery ETA (min)</label><input id="s_eta" type="number" value="${s.estimatedDeliveryMinutes}"></div>
          </div>
          <div class="switch-row"><input type="checkbox" id="s_online" ${s.onlineOrderingEnabled ? 'checked' : ''}><label style="text-transform:none">Online ordering enabled</label></div>
          <div class="switch-row"><input type="checkbox" id="s_res" ${s.reservationsEnabled ? 'checked' : ''}><label style="text-transform:none">Reservations enabled</label></div>
        </div>
      </div>
      <button class="btn primary" onclick="Admin.saveSettings()">Save Settings</button>`;
  },

  async saveSettings() {
    const s = await API.get('/api/settings');
    await API.post('/api/settings', {
      ...s,
      name: $('#s_name').value, tagline: $('#s_tag').value, address: $('#s_addr').value,
      phone: $('#s_phone').value, email: $('#s_email').value,
      taxRate: +$('#s_tax').value, serviceChargeRate: +$('#s_svc').value,
      deliveryFee: +$('#s_del').value, loyaltyPointsPerDollar: +$('#s_pts').value,
      openingHours: $('#s_hours').value, estimatedDeliveryMinutes: +$('#s_eta').value,
      onlineOrderingEnabled: $('#s_online').checked, reservationsEnabled: $('#s_res').checked
    });
    toast('Settings saved', '', 'success');
  },
});
