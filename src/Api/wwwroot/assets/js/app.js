/* زعفران — سامانه سفارش آنلاین (Persian client, vanilla JS) */
const API = location.origin + "/api/v1";
let TOKEN = localStorage.getItem("zaf_token") || null;
let MENU = [], CATS = [], CART = [], activeCat = null, BRANCH = null, trackId = null;

const $ = (s) => document.querySelector(s);

/* ── Persian digits + Toman money ── */
const FA_DIGITS = "۰۱۲۳۴۵۶۷۸۹";
const faNum = (v) => String(v).replace(/\d/g, (d) => FA_DIGITS[+d]);
const money = (v) => faNum(Number(v).toLocaleString("en-US")) + " تومان";

/* Persian labels for API status strings */
const FA_STATUS = {
  Pending: "در انتظار تأیید", Confirmed: "تأیید شده", Preparing: "در حال آماده‌سازی",
  Ready: "آماده", Served: "سرو شده", Completed: "تکمیل شده", Cancelled: "لغو شده",
  Unpaid: "پرداخت نشده", Paid: "پرداخت شده", Failed: "ناموفق", Refunded: "بازگشت وجه", PendingPayment: "در انتظار پرداخت"
};
const fa = (s) => FA_STATUS[s] || s;

async function api(path, opts = {}) {
  const res = await fetch(API + path, {
    ...opts,
    headers: { "Content-Type": "application/json", ...(TOKEN ? { Authorization: "Bearer " + TOKEN } : {}), ...(opts.headers || {}) }
  });
  if (res.status === 401) { TOKEN = null; localStorage.removeItem("zaf_token"); }
  if (!res.ok) throw new Error((await res.json()).error || res.statusText);
  return res.status === 204 ? null : res.json();
}

function toast(msg) {
  const t = $("#toast"); t.textContent = msg; t.classList.add("show");
  setTimeout(() => t.classList.remove("show"), 2600);
}

/* ── boot ── */
async function boot() {
  try {
    BRANCH = (await api("/branches"))[0];
    [CATS, MENU] = await Promise.all([api("/menu/categories"), api("/menu/items?onlyAvailable=true&branchId=" + BRANCH.id)]);
    renderCats(); renderMenu();
    const ev = await fetch(API + "/events/running?branchId=" + BRANCH.id).catch(() => null);
    if (ev && ev.ok) { const e = await ev.json(); if (e && e.isRunning) showEvent(e); }
    if (trackId) pollOrder();
  } catch (e) { toast("⚠ " + e.message); }
}

function showEvent(e) {
  const chip = document.createElement("div");
  chip.className = "event-chip";
  chip.innerHTML = `${e.bannerEmoji || "🎉"} <b>${e.title}</b> — ${faNum(e.discountPercent)}٪ تخفیف روی همه‌چیز!`;
  $("#heroContent").prepend(chip);
}

/* ── menu ── */
function renderCats() {
  const el = $("#cats");
  el.innerHTML = `<button class="active" data-id="">🍽 همه</button>` +
    CATS.map(c => `<button data-id="${c.id}">${c.emoji} ${c.name}</button>`).join("");
  el.querySelectorAll("button").forEach(b => b.onclick = () => {
    el.querySelectorAll("button").forEach(x => x.classList.remove("active"));
    b.classList.add("active"); activeCat = b.dataset.id || null; renderMenu();
  });
}

function renderMenu() {
  const items = MENU.filter(m => !activeCat || m.categoryId === activeCat);
  $("#menuGrid").innerHTML = items.map(m => `
    <article class="dish ${m.isAvailable ? "" : "soldout"}">
      <div class="thumb">${m.categoryEmoji || "🍽️"}</div>
      <div class="body">
        <div class="name">${m.name}</div>
        <div class="desc">${m.description || ""}</div>
        <div class="meta">⏱ ${faNum(m.prepMinutes)} دقیقه${m.calories ? " • 🔥 " + faNum(m.calories) + " کالری" : ""}${m.isVegetarian ? " • 🌿 گیاهی" : ""}${m.isSpicy ? " • 🌶 تند" : ""}</div>
        <div class="row">
          <span class="price">${money(m.price)}</span>
          ${m.isAvailable
            ? `<button class="btn primary add" onclick="addToCart('${m.id}')">افزودن +</button>`
            : `<span class="badge" style="background:#3a1d22;color:var(--red)">ناموجود</span>`}
        </div>
      </div>
    </article>`).join("");
}

/* ── cart ── */
function addToCart(id) {
  const line = CART.find(l => l.id === id);
  if (line) line.qty++; else CART.push({ id, qty: 1 });
  paintCart(); toast("به سبد خرید اضافه شد 🛒");
}
function chQty(id, d) {
  const line = CART.find(l => l.id === id); if (!line) return;
  line.qty += d; if (line.qty <= 0) CART = CART.filter(l => l.id !== id);
  paintCart();
}
function totals() {
  const sub = CART.reduce((s, l) => s + l.qty * MENU.find(m => m.id === l.id).price, 0);
  const disc = Math.round(sub * (window.__discount || 0) / 100);
  const tax = Math.round((sub - disc) * 9 / 100);
  return { sub, disc, tax, total: sub - disc + tax };
}
function paintCart() {
  const box = $("#cartItems");
  box.innerHTML = CART.length ? CART.map(l => {
    const m = MENU.find(x => x.id === l.id);
    return `<div class="line">
      <span>${m.categoryEmoji || "🍽️"}</span>
      <span class="n">${m.name}<br><small style="color:var(--muted)">${money(m.price)}</small></span>
      <span class="qty"><button onclick="chQty('${l.id}',-1)">−</button>${faNum(l.qty)}<button onclick="chQty('${l.id}',1)">+</button></span>
    </div>`;
  }).join("") : `<div class="empty">سبد شما خالی است — یک چیز خوشمزه اضافه کنید 🍢</div>`;
  const t = totals();
  $("#cartFoot").innerHTML = `
    <div class="tot"><span>جمع آیتم‌ها</span><span>${money(t.sub)}</span></div>
    ${t.disc ? `<div class="tot"><span>تخفیف رویداد</span><span style="color:var(--accent)">−${money(t.disc)}</span></div>` : ""}
    <div class="tot"><span>مالیات بر ارزش افزوده (۹٪)</span><span>${money(t.tax)}</span></div>
    <div class="tot grand"><span>مبلغ قابل پرداخت</span><span class="v">${money(t.total)}</span></div>
    <button class="btn primary" onclick="checkout()">پرداخت آنلاین با زرین‌پال &nbsp;→</button>`;
  const n = CART.reduce((s, l) => s + l.qty, 0);
  $("#cartCount").textContent = faNum(n || "");
  $("#cartCount").style.display = n ? "inline" : "none";
}

function toggleCart(open) {
  $("#drawer").classList.toggle("open", open);
  $("#backdrop").classList.toggle("open", open);
}

/* ── checkout ── */
async function checkout() {
  const name = $("#fName").value.trim(), phone = $("#fPhone").value.trim(), addr = $("#fAddr").value.trim();
  if (!CART.length) return toast("سبد خرید خالی است");
  if (!name || !phone) return toast("لطفاً نام و شماره تماس خود را وارد کنید");
  const type = addr ? 3 : 2;
  try {
    const order = await api("/orders", { method: "POST", body: JSON.stringify({
      type, branchId: BRANCH.id, customerName: name, customerPhone: phone,
      deliveryAddress: addr || null,
      items: CART.map(l => ({ menuItemId: l.id, quantity: l.qty }))
    })});
    const pay = await api("/payments/initiate", { method: "POST", body: JSON.stringify({ orderId: order.id, gateway: 10 }) });
    trackId = order.id; sessionStorage.setItem("zaf_track", trackId);
    toggleCart(false);
    location.href = pay.redirectUrl; // صفحه بانک (سندباکس خودکار تأیید می‌کند) → بازگشت → صفحه نتیجه
  } catch (e) { toast("⚠ " + e.message); }
}

/* ── track order ── */
async function pollOrder() {
  const box = $("#trackBox");
  const steps = [
    { en: "Pending", fa: "ثبت سفارش" }, { en: "Confirmed", fa: "تأیید شده" },
    { en: "Preparing", fa: "در حال پخت" }, { en: "Ready", fa: "آماده" }, { en: "Completed", fa: "تکمیل شد" }
  ];
  let last = "";
  const tick = async () => {
    if (!trackId) return;
    try {
      const o = await api("/orders/" + trackId);
      if (o.statusName !== last) {
        last = o.statusName;
        const idx = steps.findIndex(s => s.en === o.statusName);
        box.innerHTML = `<h2 class="section-title">سفارش #${faNum(o.orderNumber)}</h2>
          <p class="section-sub">${o.items.map(i => faNum(i.quantity) + "× " + i.itemName).join(" • ")}</p>
          <div class="track">${steps.map((s, i) => `
            <div class="step ${i <= idx ? "done" : ""}"><div class="dot"></div><div>${s.fa}</div></div>`).join("")}
          </div>
          <p style="color:var(--green);font-weight:700">مبلغ کل ${money(o.total)} — ${fa(o.paymentStatusName)}</p>`;
      }
    } catch {}
  };
  tick(); clearInterval(window.__poll); window.__poll = setInterval(tick, 4000);
}

boot();
trackId = sessionStorage.getItem("zaf_track");
if (trackId) pollOrder();
