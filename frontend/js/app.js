// ============================================================
//  Aurora — Public site: menu, cart, ordering, reservations,
//  events, reviews
// ============================================================
'use strict';

// ---------- state ----------
let MENU = [];
let CATEGORIES = [];
let PACKAGES = [];
let SETTINGS = null;
let activeCat = 0;

const cart = JSON.parse(localStorage.getItem('aurora_cart') || '[]');
let orderType = 4; // pickup default
let coupon = null; // {code, discount}
const saveCart = () => localStorage.setItem('aurora_cart', JSON.stringify(cart));

// ---------- boot ----------
document.addEventListener('DOMContentLoaded', async () => {
  navScroll();
  revealOnScroll();
  loadSettings();
  loadCategories();
  loadMenu();
  loadPackages();
  loadReviews();
  wireCart();
  wireForms();
  const today = new Date().toISOString().split('T')[0];
  ['rvDate', 'evDate'].forEach(id => { const el = document.getElementById(id); if (el) el.value = today; });
});

// ---------- nav ----------
window.addEventListener('scroll', () => $('#nav').classList.toggle('scrolled', scrollY > 40));

function revealOnScroll() {
  const io = new IntersectionObserver(entries => entries.forEach(e => { if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); } }), { threshold: 0.12 });
  document.querySelectorAll('.reveal').forEach(el => io.observe(el));
}

async function loadSettings() {
  try {
    SETTINGS = await API.get('/api/settings', true);
    if (SETTINGS.openingHours) { const el = $('#statHours'); if (el) el.textContent = SETTINGS.openingHours; }
    if (SETTINGS.address) $('#footAddress').textContent = SETTINGS.address;
    if (SETTINGS.phone) $('#footPhone').textContent = SETTINGS.phone;
    if (SETTINGS.openingHours) $('#footHours').textContent = `Open daily ${SETTINGS.openingHours}`;
  } catch (_) {}
}

// ---------- menu ----------
async function loadCategories() {
  try {
    CATEGORIES = await API.get('/api/menu/categories', true);
    renderTabs();
  } catch (_) { toast('Menu', 'Failed to load categories', 'error'); }
}

async function loadMenu() {
  const grid = $('#menuGrid');
  grid.innerHTML = Array(6).fill('<div class="dish-card"><div class="dish-img skeleton"></div><div class="dish-body"><div class="skeleton" style="height:18px;width:60%"></div><div class="skeleton" style="height:14px;width:90%"></div></div></div>').join('');
  try {
    MENU = await API.get('/api/menu?includeUnavailable=false', true);
    renderMenu();
  } catch (_) { grid.innerHTML = '<p style="color:#9aa0ab">Menu unavailable — please refresh.</p>'; }
}

function renderTabs() {
  const tabs = $('#menuTabs');
  tabs.innerHTML = `<button class="tab active" data-cat="0">All</button>` +
    CATEGORIES.map(c => `<button class="tab" data-cat="${c.id}">${esc(c.name)}</button>`).join('');
  tabs.querySelectorAll('.tab').forEach(b => b.onclick = () => {
    tabs.querySelectorAll('.tab').forEach(x => x.classList.remove('active'));
    b.classList.add('active');
    activeCat = +b.dataset.cat;
    renderMenu();
  });
}

function renderMenu() {
  const grid = $('#menuGrid');
  const list = activeCat === 0 ? MENU : MENU.filter(m => m.categoryId === activeCat);
  $('#statDishes').textContent = `${MENU.length}+`;
  if (!list.length) { grid.innerHTML = '<p style="grid-column:1/-1;text-align:center;color:#9aa0ab">No dishes in this category yet.</p>'; return; }
  grid.innerHTML = list.map(m => {
    const badges = [
      m.isFeatured ? '<span class="badge b-feat">Chef\'s Pick</span>' : '',
      m.isVegetarian ? '<span class="badge b-veg">Veg</span>' : '',
      m.isSpicy ? '<span class="badge b-spicy">Spicy</span>' : '',
      m.isGlutenFree ? '<span class="badge b-gf">GF</span>' : ''
    ].join('');
    return `
    <article class="dish-card reveal in">
      <div class="dish-img">
        <img src="${esc(m.imageUrl || '/assets/img/dish-default.svg')}" alt="${esc(m.name)}" loading="lazy"
             onerror="this.src='/assets/img/dish-default.svg'">
        <div class="dish-badges">${badges}</div>
      </div>
      <div class="dish-body">
        <div class="dish-cat">${esc(m.categoryName)}</div>
        <h3 class="dish-name">${esc(m.name)}</h3>
        <p class="dish-desc">${esc(m.description || '')}</p>
        <div class="dish-meta"><span>~${m.prepTimeMinutes} min</span>${m.calories ? `<span>${m.calories} kcal</span>` : ''}${m.allergens ? `<span>contains: ${esc(m.allergens)}</span>` : ''}</div>
        <div class="dish-foot">
          <div class="dish-price">${money(m.price)}</div>
          <button class="add-btn" data-id="${m.id}">Add +</button>
        </div>
      </div>
    </article>`;
  }).join('');
  grid.querySelectorAll('.add-btn').forEach(b => b.onclick = e => { e.stopPropagation(); openDishModal(+b.dataset.id); });
  grid.querySelectorAll('.dish-card').forEach(c => c.onclick = () => openDishModal(+c.querySelector('.add-btn').dataset.id));
}

// ---------- dish modal (modifiers) ----------
let dishModalItem = null;
let dishSelections = {};

function openDishModal(id) {
  const m = MENU.find(x => x.id === id);
  if (!m) return;
  dishModalItem = m;
  dishSelections = {};
  $('#dmCat').textContent = m.categoryName.toUpperCase();
  $('#dmName').textContent = m.name;
  $('#dmDesc').textContent = m.description || '';
  $('#dmNotes').value = '';

  const modWrap = $('#dmModifiers');
  if (m.modifierGroups?.length) {
    modWrap.innerHTML = m.modifierGroups.map(g => `
      <div class="mod-group">
        <div class="mod-title">${esc(g.name)} ${g.maxSelection === 1 ? '<small style="color:#9aa0ab">(choose 1)</small>' : `(up to ${g.maxSelection})`}</div>
        <div class="mod-opts" data-gid="${g.id}">
          ${g.options.map(o => `<button class="mod-opt" data-gid="${g.id}" data-oid="${o.id}" data-price="${o.extraPrice}">${esc(o.name)}${o.extraPrice ? ` +${money(o.extraPrice)}` : ''}</button>`).join('')}
        </div>
      </div>`).join('');
    modWrap.querySelectorAll('.mod-opt').forEach(btn => btn.onclick = () => {
      const gid = btn.dataset.gid;
      const group = m.modifierGroups.find(g => String(g.id) === gid);
      if (group.maxSelection === 1) {
        modWrap.querySelectorAll(`.mod-opt[data-gid="${gid}"]`).forEach(b => b.classList.remove('selected'));
        dishSelections[gid] = [btn];
      } else {
        const sel = modWrap.querySelectorAll(`.mod-opt[data-gid="${gid}"].selected`);
        if (btn.classList.contains('selected')) btn.classList.remove('selected');
        else if (sel.length < group.maxSelection) btn.classList.add('selected');
      }
    });
  } else modWrap.innerHTML = '';

  updateDishModalPrice();
  $('#dishModal').classList.add('show');
}

function updateDishModalPrice() {
  const extras = currentModifierExtra();
  $('#dmPrice').textContent = money((dishModalItem?.price || 0) + extras);
}

function currentModifierExtra() {
  if (!dishModalItem) return 0;
  let extra = 0;
  $('#dmModifiers').querySelectorAll('.mod-opt.selected').forEach(b => extra += parseFloat(b.dataset.price || 0));
  return extra;
}

function closeDishModal() { $('#dishModal').classList.remove('show'); }

window.closeDishModal = closeDishModal;
document.addEventListener('DOMContentLoaded', () => {
  $('#dmAdd').onclick = () => {
    const mods = [];
    $('#dmModifiers').querySelectorAll('.mod-opt.selected').forEach(b => {
      const label = b.textContent.split(' +')[0].trim();
      mods.push(label);
    });
    addToCart(dishModalItem, mods, $('#dmNotes').value.trim(), currentModifierExtra());
    closeDishModal();
  };
  $('#dishModal').addEventListener('click', e => { if (e.target.id === 'dishModal') closeDishModal(); });
  $('#dmModifiers').addEventListener('click', updateDishModalPrice);
});

// ---------- cart ----------
function addToCart(item, mods, notes, extraPrice = 0) {
  const key = `${item.id}|${mods.join(',')}`;
  const existing = cart.find(c => c.key === key);
  if (existing) existing.qty++;
  else cart.push({ key, id: item.id, name: item.name, price: item.price + extraPrice, qty: 1, mods, notes });
  saveCart();
  renderCart();
  toast('Added to cart', `${item.name}${mods.length ? ' · ' + mods.join(', ') : ''}`, 'success');
}

function cartSubtotal() { return cart.reduce((s, c) => s + c.price * c.qty, 0); }

function renderCart() {
  const count = cart.reduce((s, c) => s + c.qty, 0);
  const cc = $('#cartCount');
  cc.textContent = count;
  cc.classList.toggle('show', count > 0);

  const body = $('#cartBody');
  if (!cart.length) {
    body.innerHTML = `<div class="cart-empty"><div class="big">A</div><b>Your cart is empty</b><p>Add something delicious from the menu.</p></div>`;
  } else {
    body.innerHTML = cart.map((c, i) => `
      <div class="cart-item">
        <div class="ci-info">
          <b>${esc(c.name)}</b>
          ${c.mods?.length ? `<span class="ci-mod">${esc(c.mods.join(', '))}</span>` : ''}
          ${c.notes ? `<span class="ci-notes">"${esc(c.notes)}"</span>` : ''}
          <div class="ci-price">${money(c.price)} each</div>
        </div>
        <div class="qty-box">
          <button data-i="${i}" data-d="-1">−</button><span>${c.qty}</span><button data-i="${i}" data-d="1">+</button>
        </div>
      </div>`).join('');
    body.querySelectorAll('.qty-box button').forEach(b => b.onclick = () => {
      const i = +b.dataset.i;
      cart[i].qty += +b.dataset.d;
      if (cart[i].qty <= 0) cart.splice(i, 1);
      saveCart(); renderCart();
    });
  }
  updateTotals();
}

function updateTotals() {
  const sub = cartSubtotal();
  const taxRate = SETTINGS ? SETTINGS.taxRate : 0.09;
  const delFee = orderType === 3 ? (SETTINGS?.deliveryFee ?? 3.5) : 0;
  const disc = coupon?.discount || 0;
  const tax = Math.max(0, (sub - disc)) * taxRate;
  $('#tSub').textContent = money(sub);
  $('#tDiscRow').style.display = disc ? 'flex' : 'none';
  $('#tDisc').textContent = '-' + money(disc);
  $('#tTax').textContent = money(tax);
  $('#tDelRow').style.display = orderType === 3 ? 'flex' : 'none';
  $('#tDel').textContent = money(delFee);
  $('#tTotal').textContent = money(Math.max(0, sub - disc + tax + delFee));
}

function wireCart() {
  renderCart();
  $('#cartBtn').onclick = () => { $('#cartDrawer').classList.add('open'); $('#overlay').classList.add('show'); };
  $('#closeCart').onclick = closeCart;
  $('#overlay').onclick = closeCart;
  function closeCart() { $('#cartDrawer').classList.remove('open'); $('#overlay').classList.remove('show'); }

  $$('.order-type-seg button').forEach(b => b.onclick = () => {
    $$('.order-type-seg button').forEach(x => x.classList.remove('active'));
    b.classList.add('active');
    orderType = +b.dataset.type;
    $('#deliveryFields').style.display = orderType === 3 ? 'block' : 'none';
    updateTotals();
  });

  $('#applyCoupon').onclick = async () => {
    const code = $('#couponInput').value.trim();
    if (!code) return;
    try {
      const res = await API.post('/api/coupons/validate', { code, orderAmount: cartSubtotal() }, true);
      if (res.valid) { coupon = { code: code.toUpperCase(), discount: res.discount }; toast('Coupon applied', res.message, 'success'); }
      else { coupon = null; toast('Coupon rejected', res.message, 'error'); }
      updateTotals();
    } catch (e) { toast('Error', e.message, 'error'); }
  };

  $('#placeOrder').onclick = placeOrder;
}

async function placeOrder() {
  if (!cart.length) return toast('Cart is empty', 'Add some dishes first', 'error');
  const phone = $('#ordPhone').value.trim();
  if (!phone) return toast('Phone required', 'We need a phone number for the order', 'error');
  if (orderType === 3 && !$('#ordAddress').value.trim()) return toast('Address required', 'Delivery needs an address', 'error');

  const btn = $('#placeOrder');
  btn.disabled = true; btn.textContent = 'Placing order…';
  try {
    const order = await API.post('/api/orders', {
      type: orderType,
      tableId: null,
      guestName: $('#ordName').value.trim() || 'Online guest',
      phone,
      deliveryAddress: orderType === 3 ? $('#ordAddress').value.trim() : null,
      couponCode: coupon?.code || null,
      notes: $('#ordNotes').value.trim() || null,
      items: cart.map(c => ({ menuItemId: c.id, quantity: c.qty, notes: c.notes || null, modifierText: c.mods?.join(', ') || null }))
    }, true);
    cart.length = 0; coupon = null; saveCart(); renderCart();
    $('#cartDrawer').classList.remove('open'); $('#overlay').classList.remove('show');
    toast('Order placed!', `${order.orderNumber} — estimated ready in ${new Date(order.estimatedReadyTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`, 'success');
  } catch (e) {
    toast('Order failed', e.message, 'error');
  } finally {
    btn.disabled = false; btn.textContent = 'Place Order';
  }
}

// ---------- events ----------
async function loadPackages() {
  try {
    PACKAGES = await API.get('/api/events/packages', true);
    const grid = $('#eventsGrid');
    grid.innerHTML = PACKAGES.map(p => `
      <article class="event-card reveal in">
        <div class="event-icon">${p.name[0]}</div>
        <h3>${esc(p.name)}</h3>
        <p>${esc(p.description || '')}</p>
        <div class="event-price">${money(p.pricePerPerson)} <small>/ person · min ${p.minGuests} guests</small></div>
        <ul class="event-includes">${(p.includedServices || '').split(',').map(s => `<li>${esc(s.trim())}</li>`).join('')}</ul>
        <a href="#reservations" class="btn ghost" style="padding:10px 22px;">Plan This Event</a>
      </article>`).join('');
    const sel = $('#evPackage');
    sel.innerHTML = '<option value="">No package / custom</option>' + PACKAGES.map(p => `<option value="${p.id}">${esc(p.name)} — ${money(p.pricePerPerson)}/person</option>`).join('');
    $('#evGuests').addEventListener('input', updateQuoteHint);
    $('#evPackage').addEventListener('change', updateQuoteHint);
  } catch (_) {}
}

function updateQuoteHint() {
  const pkg = PACKAGES.find(p => String(p.id) === $('#evPackage').value);
  const guests = +$('#evGuests').value || 0;
  if (pkg && guests >= pkg.minGuests) $('#evQuoteHint').textContent = `Estimated quote: ${money(pkg.pricePerPerson * guests)}`;
  else if (pkg) $('#evQuoteHint').textContent = `This package needs at least ${pkg.minGuests} guests.`;
  else $('#evQuoteHint').textContent = '';
}

// ---------- reviews ----------
async function loadReviews() {
  try {
    const reviews = await API.get('/api/reviews/public', true);
    $('#reviewsGrid').innerHTML = reviews.map(r => `
      <article class="review-card reveal in">
        <div class="stars">${'★'.repeat(r.rating)}${'☆'.repeat(5 - r.rating)}</div>
        <p class="review-text">"${esc(r.comment || '')}"</p>
        ${r.reply ? `<div class="reply"><b>Management:</b> ${esc(r.reply)}</div>` : ''}
        <div class="review-meta">
          <div class="avatar">${esc(initials(r.customerName))}</div>
          <div><div class="name">${esc(r.customerName)}</div><div class="type">${esc(r.typeName)}</div></div>
        </div>
      </article>`).join('') || '<p style="color:#9aa0ab">Be the first to review us.</p>';
  } catch (_) {}
}

// ---------- forms ----------
function wireForms() {
  $('#reservationForm').onsubmit = async e => {
    e.preventDefault();
    const btn = e.target.querySelector('button');
    btn.disabled = true;
    try {
      await API.post('/api/reservations', {
        customerName: $('#rvName').value.trim(), phone: $('#rvPhone').value.trim(),
        partySize: +$('#rvSize').value,
        dateTime: new Date(`${$('#rvDate').value}T${$('#rvTime').value}`).toISOString(),
        occasion: $('#rvOccasion').value || null,
        notes: $('#rvNotes').value.trim() || null,
        source: 'Website'
      }, true);
      toast('Table requested!', 'We will confirm shortly — check your phone.', 'success');
      e.target.reset();
    } catch (err) { toast('Reservation failed', err.message, 'error'); }
    finally { btn.disabled = false; }
  };

  $('#eventForm').onsubmit = async e => {
    e.preventDefault();
    const btn = e.target.querySelector('button');
    btn.disabled = true;
    try {
      await API.post('/api/events/bookings', {
        customerName: $('#evName').value.trim(), phone: $('#evPhone').value.trim(),
        email: $('#evEmail').value.trim() || null, type: +$('#evType').value,
        packageId: $('#evPackage').value ? +$('#evPackage').value : null,
        eventDate: $('#evDate').value, startTime: $('#evTime').value || null,
        guestCount: +$('#evGuests').value, venue: 1,
        menuNotes: $('#evNotes').value.trim() || null, notes: null
      }, true);
      toast('Inquiry sent!', 'Our events team will contact you with a full quote.', 'success');
      e.target.reset(); updateQuoteHint();
    } catch (err) { toast('Inquiry failed', err.message, 'error'); }
    finally { btn.disabled = false; }
  };

  $('#reviewForm').onsubmit = async e => {
    e.preventDefault();
    const btn = e.target.querySelector('button');
    btn.disabled = true;
    try {
      await API.post('/api/reviews', {
        customerName: $('#rwName').value.trim(), rating: +$('#rwRating').value,
        comment: $('#rwComment').value.trim(), type: +$('#rwType').value
      }, true);
      toast('Thank you!', 'Your review is pending moderation.', 'success');
      e.target.reset();
    } catch (err) { toast('Failed', err.message, 'error'); }
    finally { btn.disabled = false; }
  };
}
