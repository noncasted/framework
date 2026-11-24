"use strict";

// A zoomable, pannable image inside a box. The view {k, cx, cy}: k is the zoom over "fit to the box", (cx, cy) the
// point of the image (0..1) at the centre of the box. Several viewports may share one view object (synced zoom).
class Viewport {
  constructor(element, handlers = {}) {
    this.element = element;
    this.img = element.querySelector("img");
    this.badge = element.querySelector(".zoom");
    this.handlers = handlers;
    this.view = { k: 1, cx: 0.5, cy: 0.5 };
    this.size = null;
    this.frames = [];
    this.timer = 0;
    this.img.addEventListener("load", () => {
      if (!this.size) {
        this.size = { w: this.img.naturalWidth || 512, h: this.img.naturalHeight || 512 };
        this.handlers.onSize?.(this.size);
      }
      this.layout();
    });
    element.addEventListener("wheel", (e) => this.onWheel(e), { passive: false });
    element.addEventListener("pointerdown", (e) => this.onPointerDown(e));
    // No autoscroll or paste of the middle button.
    element.addEventListener("mousedown", (e) => { if (e.button === 1) e.preventDefault(); });
    element.addEventListener("auxclick", (e) => e.preventDefault());
    element.addEventListener("dblclick", (e) => { e.preventDefault(); this.handlers.onDoubleClick?.(e); });
    new ResizeObserver(() => this.layout()).observe(element);
  }

  show(frames, delay) {
    clearInterval(this.timer);
    this.size = null;
    this.frames = frames;
    this.img.src = frames[0];
    if (frames.length > 1) {
      for (const src of frames) new Image().src = src;
      let index = 0;
      this.timer = setInterval(() => { index = (index + 1) % frames.length; this.img.src = frames[index]; }, delay);
    }
  }

  stop() { clearInterval(this.timer); }

  fit() { return this.size ? Math.min(this.element.clientWidth / this.size.w, this.element.clientHeight / this.size.h) : 1; }
  scale() { return this.fit() * this.view.k; }

  layout() {
    if (!this.size) return;
    const s = this.scale();
    const w = this.size.w * s, h = this.size.h * s;
    const left = this.element.clientWidth / 2 - this.view.cx * w;
    const top = this.element.clientHeight / 2 - this.view.cy * h;
    this.img.style.width = `${w}px`;
    this.img.style.height = `${h}px`;
    this.img.style.transform = `translate(${left}px, ${top}px)`;
    const devicePercent = s * devicePixelRatio * 100;
    this.img.classList.toggle("pixelated", devicePercent >= 199.5);
    if (this.badge) this.badge.textContent = `${this.size.w}×${this.size.h}  ${formatPercent(devicePercent)}`;
  }

  // Zoom by factor keeping the image point under (x, y) of the box in place.
  zoomAt(factor, x, y) {
    if (!this.size) return;
    const s = this.scale();
    const k = clamp(this.view.k * factor, 0.05, 400);
    const s2 = this.fit() * k;
    const w = this.size.w, h = this.size.h;
    const left = this.element.clientWidth / 2 - this.view.cx * w * s;
    const top = this.element.clientHeight / 2 - this.view.cy * h * s;
    const ix = (x - left) / s, iy = (y - top) / s;
    const left2 = x - ix * s2, top2 = y - iy * s2;
    this.view.k = k;
    this.view.cx = (this.element.clientWidth / 2 - left2) / (w * s2);
    this.view.cy = (this.element.clientHeight / 2 - top2) / (h * s2);
    this.handlers.onChange?.();
  }

  zoomCenter(factor) { this.zoomAt(factor, this.element.clientWidth / 2, this.element.clientHeight / 2); }

  reset() { Object.assign(this.view, { k: 1, cx: 0.5, cy: 0.5 }); this.handlers.onChange?.(); }

  pixel() {
    if (!this.size) return;
    this.view.k = 1 / (this.fit() * devicePixelRatio);
    this.handlers.onChange?.();
  }

  onWheel(e) {
    e.preventDefault();
    const delta = e.deltaY * (e.deltaMode === 1 ? 16 : 1);
    const rect = this.element.getBoundingClientRect();
    this.zoomAt(Math.exp(-delta * 0.0015), e.clientX - rect.left, e.clientY - rect.top);
  }

  // Left button: drag pans, a click without a move is a click. Middle button (the wheel pressed): drag pans only.
  onPointerDown(e) {
    if (e.button !== 0 && e.button !== 1) return;
    const middle = e.button === 1;
    e.preventDefault();
    const startX = e.clientX, startY = e.clientY;
    let lastX = startX, lastY = startY, moved = false;
    this.element.setPointerCapture(e.pointerId);
    const move = (m) => {
      if (!moved && Math.hypot(m.clientX - startX, m.clientY - startY) < 4) return;
      moved = true;
      this.element.classList.add("dragging");
      if (this.size) {
        const s = this.scale();
        this.view.cx -= (m.clientX - lastX) / (this.size.w * s);
        this.view.cy -= (m.clientY - lastY) / (this.size.h * s);
        this.handlers.onChange?.();
      }
      lastX = m.clientX;
      lastY = m.clientY;
    };
    const up = (u) => {
      this.element.removeEventListener("pointermove", move);
      this.element.removeEventListener("pointerup", up);
      this.element.classList.remove("dragging");
      if (!moved && !middle) this.handlers.onClick?.(u);
    };
    this.element.addEventListener("pointermove", move);
    this.element.addEventListener("pointerup", up);
  }
}

function clamp(v, lo, hi) { return Math.min(hi, Math.max(lo, v)); }
function formatPercent(p) { return p >= 10 ? `${Math.round(p)}%` : `${p.toFixed(1)}%`; }
function post(message) { window.webkit.messageHandlers.cv.postMessage(message); }
const $ = (id) => document.getElementById(id);
function viewportElement() {
  const element = document.createElement("div");
  element.className = "viewport";
  element.innerHTML = '<img alt=""><span class="zoom"></span>';
  return element;
}

