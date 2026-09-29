// ============================================================
//  Aurora — Tiny dependency-free canvas chart helpers
// ============================================================
'use strict';

const CHART_COLORS = ['#4f46e5', '#0ea5a4', '#f59e0b', '#ec4899', '#3b82f6', '#22c55e', '#ef4444', '#8b5cf6'];

const Charts = {
  prep(canvas, height = 240) {
    const dpr = window.devicePixelRatio || 1;
    const w = canvas.parentElement.clientWidth || 600;
    canvas.width = w * dpr; canvas.height = height * dpr;
    canvas.style.height = height + 'px';
    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, w, height);
    return { ctx, w, h: height };
  },

  // vertical bar chart — data: [{label, value}]
  bars(canvas, data, { color = '#4f46e5', height = 240, currency = false } = {}) {
    if (!canvas || !data?.length) return;
    const { ctx, w, h } = this.prep(canvas, height);
    const pad = { l: 46, r: 12, t: 18, b: 34 };
    const max = Math.max(...data.map(d => d.value), 1) * 1.12;
    const iw = w - pad.l - pad.r, ih = h - pad.t - pad.b;
    const bw = Math.min(46, iw / data.length * 0.6);
    // gridlines
    ctx.strokeStyle = '#eef1f6'; ctx.fillStyle = '#9aa3b8'; ctx.font = '11px Inter'; ctx.textAlign = 'right';
    for (let i = 0; i <= 4; i++) {
      const y = pad.t + ih - ih * i / 4;
      ctx.beginPath(); ctx.moveTo(pad.l, y); ctx.lineTo(w - pad.r, y); ctx.stroke();
      const val = max * i / 4;
      ctx.fillText(currency ? '$' + Math.round(val) : Math.round(val), pad.l - 8, y + 4);
    }
    // bars
    data.forEach((d, i) => {
      const x = pad.l + iw / data.length * (i + 0.5) - bw / 2;
      const bh = ih * (d.value / max);
      const y = pad.t + ih - bh;
      const grad = ctx.createLinearGradient(0, y, 0, pad.t + ih);
      grad.addColorStop(0, color); grad.addColorStop(1, color + '55');
      ctx.fillStyle = grad;
      const r = Math.min(7, bw / 2, bh);
      ctx.beginPath();
      ctx.moveTo(x, pad.t + ih);
      ctx.lineTo(x, y + r); ctx.arcTo(x, y, x + r, y, r);
      ctx.lineTo(x + bw - r, y); ctx.arcTo(x + bw, y, x + bw, y + r, r);
      ctx.lineTo(x + bw, pad.t + ih); ctx.closePath(); ctx.fill();
      ctx.fillStyle = '#6b7385'; ctx.textAlign = 'center';
      ctx.font = '10.5px Inter';
      ctx.fillText(d.label.length > 9 ? d.label.slice(0, 9) : d.label, x + bw / 2, h - 12);
    });
  },

  // smooth line chart — data: [{label, value}]
  line(canvas, data, { color = '#0ea5a4', height = 240, currency = false } = {}) {
    if (!canvas || !data?.length) return;
    const { ctx, w, h } = this.prep(canvas, height);
    const pad = { l: 46, r: 14, t: 18, b: 34 };
    const max = Math.max(...data.map(d => d.value), 1) * 1.15;
    const iw = w - pad.l - pad.r, ih = h - pad.t - pad.b;
    const pts = data.map((d, i) => ({
      x: pad.l + (data.length === 1 ? iw / 2 : iw * i / (data.length - 1)),
      y: pad.t + ih - ih * (d.value / max), v: d.value, label: d.label
    }));
    ctx.strokeStyle = '#eef1f6'; ctx.fillStyle = '#9aa3b8'; ctx.font = '11px Inter'; ctx.textAlign = 'right';
    for (let i = 0; i <= 4; i++) {
      const y = pad.t + ih - ih * i / 4;
      ctx.beginPath(); ctx.moveTo(pad.l, y); ctx.lineTo(w - pad.r, y); ctx.stroke();
      const val = max * i / 4;
      ctx.fillText(currency ? '$' + Math.round(val) : Math.round(val), pad.l - 8, y + 4);
    }
    // area fill
    const grad = ctx.createLinearGradient(0, pad.t, 0, pad.t + ih);
    grad.addColorStop(0, color + '40'); grad.addColorStop(1, color + '00');
    ctx.beginPath();
    pts.forEach((p, i) => i === 0 ? ctx.moveTo(p.x, p.y) : ctx.lineTo(p.x, p.y));
    ctx.lineTo(pts[pts.length - 1].x, pad.t + ih); ctx.lineTo(pts[0].x, pad.t + ih); ctx.closePath();
    ctx.fillStyle = grad; ctx.fill();
    // stroke
    ctx.beginPath();
    pts.forEach((p, i) => i === 0 ? ctx.moveTo(p.x, p.y) : ctx.lineTo(p.x, p.y));
    ctx.strokeStyle = color; ctx.lineWidth = 2.5; ctx.lineJoin = 'round'; ctx.stroke();
    // points
    pts.forEach(p => {
      ctx.beginPath(); ctx.arc(p.x, p.y, 3.6, 0, Math.PI * 2);
      ctx.fillStyle = '#fff'; ctx.fill(); ctx.strokeStyle = color; ctx.lineWidth = 2.2; ctx.stroke();
    });
    // x labels (skip to avoid crowding)
    ctx.fillStyle = '#6b7385'; ctx.font = '10.5px Inter'; ctx.textAlign = 'center';
    const skip = Math.ceil(data.length / 8);
    pts.forEach((p, i) => { if (i % skip === 0 || i === pts.length - 1) ctx.fillText(p.label, p.x, h - 12); });
  },

  // donut — data: [{label, value}]
  donut(canvas, data, { height = 230, center } = {}) {
    if (!canvas || !data?.length) return;
    const { ctx, w, h } = this.prep(canvas, height);
    const total = data.reduce((s, d) => s + d.value, 0) || 1;
    const cx = w / 2, cy = h / 2, r = Math.min(w, h) / 2 - 16, thickness = Math.max(24, r * 0.36);
    let angle = -Math.PI / 2;
    data.forEach((d, i) => {
      const slice = d.value / total * Math.PI * 2;
      ctx.beginPath();
      ctx.arc(cx, cy, r, angle, angle + slice - 0.02);
      ctx.arc(cx, cy, r - thickness, angle + slice - 0.02, angle, true);
      ctx.closePath();
      ctx.fillStyle = CHART_COLORS[i % CHART_COLORS.length];
      ctx.fill();
      angle += slice;
    });
    if (center) {
      ctx.fillStyle = '#1c2333'; ctx.font = '800 20px Inter'; ctx.textAlign = 'center';
      ctx.fillText(center, cx, cy + 2);
    }
  }
};

function legendHtml(data) {
  const total = data.reduce((s, d) => s + d.value, 0) || 1;
  return `<div class="legend">` + data.map((d, i) =>
    `<span class="lg"><span class="sw" style="background:${CHART_COLORS[i % CHART_COLORS.length]}"></span>${d.label} — ${d.value}${d.value <= 100 && Number.isInteger(d.value) ? '' : ''} (${Math.round(d.value / total * 100)}%)</span>`
  ).join('') + `</div>`;
}
