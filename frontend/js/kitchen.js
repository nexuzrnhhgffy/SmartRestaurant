// ============================================================
//  Aurora Kitchen Display System — realtime queue (polling)
// ============================================================
'use strict';

let STATION = 'all';
let lastTicketIds = new Set();
let firstLoad = true;
let audioCtx = null;

function beep() {
  try {
    audioCtx = audioCtx || new (window.AudioContext || window.webkitAudioContext)();
    const o = audioCtx.createOscillator(), g = audioCtx.createGain();
    o.connect(g); g.connect(audioCtx.destination);
    o.frequency.value = 880; o.type = 'sine';
    g.gain.setValueAtTime(0.18, audioCtx.currentTime);
    g.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + 0.45);
    o.start(); o.stop(audioCtx.currentTime + 0.5);
  } catch (_) {}
}

function kdsToast(msg) {
  const el = document.createElement('div');
  el.className = 'kds-toast';
  el.textContent = msg;
  document.body.appendChild(el);
  setTimeout(() => el.remove(), 4000);
}

document.getElementById('stationFilter').addEventListener('click', e => {
  const btn = e.target.closest('button');
  if (!btn) return;
  document.querySelectorAll('#stationFilter button').forEach(b => b.classList.remove('active'));
  btn.classList.add('active');
  STATION = btn.dataset.st;
  load();
});

document.addEventListener('DOMContentLoaded', () => {
  // gate: KDS requires chef/manager/superadmin token
  const token = localStorage.getItem('aurora_token') || sessionStorage.getItem('aurora_token');
  if (!token) { location.href = '/admin.html'; return; }
  tickClock();
  setInterval(tickClock, 1000);
  load();
  setInterval(load, 5000);
});

function tickClock() {
  document.getElementById('clock').textContent = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

async function load() {
  try {
    const q = await API.get('/api/kitchen/queue');
    const tickets = [...q.active, ...q.ready];
    // new ticket chime
    if (!firstLoad) {
      const newIds = tickets.filter(t => !lastTicketIds.has(t.orderId));
      if (newIds.length) { beep(); kdsToast(`${newIds.length} new ticket${newIds.length > 1 ? 's' : ''} fired!`); }
    }
    firstLoad = false;
    lastTicketIds = new Set(tickets.map(t => t.orderId));

    const newQ = q.active.filter(t => t.lines.some(l => l.statusName === 'Pending'));
    const wipQ = q.active.filter(t => t.lines.some(l => l.statusName === 'Preparing') && !newQ.includes(t));
    const readyQ = q.ready;

    const late = tickets.filter(t => t.minutesElapsed > 20).length;
    const avg = tickets.length ? Math.round(tickets.reduce((s, t) => s + t.minutesElapsed, 0) / tickets.length) : 0;
    document.getElementById('stActive').textContent = tickets.length;
    document.getElementById('stReady').textContent = readyQ.length;
    document.getElementById('stLate').textContent = late;
    document.getElementById('stAvg').textContent = avg + 'm';

    document.getElementById('cntNew').textContent = newQ.length;
    document.getElementById('cntWip').textContent = wipQ.length;
    document.getElementById('cntReady').textContent = readyQ.length;

    document.getElementById('colNew').innerHTML = newQ.length ? newQ.map(renderTicket).join('') : emptyCol('All caught up');
    document.getElementById('colWip').innerHTML = wipQ.length ? wipQ.map(renderTicket).join('') : emptyCol('Nothing on the fire');
    document.getElementById('colReady').innerHTML = readyQ.length ? readyQ.map(renderReady).join('') : emptyCol('Pass is clear');
  } catch (e) {
    if (String(e.message).includes('401') || String(e.message).includes('Session')) location.href = '/admin.html';
  }
}

function emptyCol(msg) {
  return `<div class="kds-empty"><div class="big">✓</div>${msg}</div>`;
}

function renderTicket(t) {
  const late = t.minutesElapsed > 20, warn = t.minutesElapsed > 12 && !late;
  const lines = t.lines.filter(l => STATION === 'all' || String(l.station) === STATION || l.statusName !== 'Pending');
  return `
  <div class="ticket ${late ? 'late' : warn ? 'warn' : ''}">
    <div class="tk-head">
      <div>
        <div class="tk-num">${esc(t.orderNumber)}</div>
        <div class="tk-type">${t.typeName}${t.tableNumber ? ' · Table ' + t.tableNumber : ''} · ${fmt.time(t.createdAt)}</div>
      </div>
      <div class="tk-timer ${late ? 'late' : warn ? 'warn' : ''}">${t.minutesElapsed}m</div>
    </div>
    <div class="tk-lines">${t.lines.map(l => `
      <div class="tk-line ${l.statusName === 'Ready' ? 'line-ready' : ''}">
        <span class="qty">${l.quantity}×</span>
        <div class="name">${esc(l.itemName)}
          ${l.modifierText ? `<span class="mod">▸ ${esc(l.modifierText)}</span>` : ''}
          ${l.notes ? `<span class="note">✎ ${esc(l.notes)}</span>` : ''}
        </div>
        <span class="line-status ${l.statusName === 'Ready' ? 'l-ready' : l.statusName === 'Preparing' ? 'l-preparing' : ''}">${l.statusName}</span>
      </div>`).join('')}
    </div>
    <div class="tk-foot">
      ${t.lines.some(l => l.statusName === 'Pending') ? `<button class="start" onclick="bump(${t.orderId},1,2)">Start Cooking</button>` : ''}
      ${t.lines.some(l => l.statusName === 'Preparing') ? `<button class="ready" onclick="bump(${t.orderId},2,3)">All Ready</button>` : ''}
    </div>
  </div>`;
}

function renderReady(t) {
  return `
  <div class="ticket done">
    <div class="tk-head">
      <div>
        <div class="tk-num">${esc(t.orderNumber)}</div>
        <div class="tk-type">${t.typeName}${t.tableNumber ? ' · Table ' + t.tableNumber : ''}</div>
      </div>
      <div class="tk-timer">${t.minutesElapsed}m</div>
    </div>
    <div class="tk-lines">${t.lines.map(l => `
      <div class="tk-line line-ready">
        <span class="qty">${l.quantity}×</span>
        <div class="name">${esc(l.itemName)}</div>
        <span class="line-status l-ready">Ready</span>
      </div>`).join('')}
    </div>
    <div class="tk-foot">
      <button class="bump" onclick="serveTicket(${t.orderId})">BUMP — Served</button>
    </div>
  </div>`;
}

async function bump(orderId, fromStatus, toStatus) {
  const q = await API.get('/api/kitchen/queue');
  const ticket = [...q.active, ...q.ready].find(t => t.orderId === orderId);
  if (!ticket) return;
  for (const line of ticket.lines.filter(l => l.status === fromStatus)) {
    await API.post(`/api/orders/items/${line.orderItemId}/status`, { status: toStatus });
  }
  if (toStatus === 3) beep();
  load();
}

async function serveTicket(orderId) {
  await API.post(`/api/orders/${orderId}/status`, { status: 5 });
  kdsToast('Ticket bumped — served');
  load();
}
