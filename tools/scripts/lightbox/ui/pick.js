"use strict";

// The grid of the variants of the open pick, and one variant at a time, large.

// --------------------------------------------------------------------------------------------------------- the pick

let pick = null;   // {id, title, from, delay, items, closed}
let tiles = [];    // {item, element, viewport, check}
let columns = 3;
let shared = { k: 1, cx: 0.5, cy: 0.5 };

function startPick(next) {
  for (const tile of tiles) tile.viewport.stop();
  closeDetail();
  pick = { ...next, closed: false };
  shared = { k: 1, cx: 0.5, cy: 0.5 };
  $("pick").classList.remove("closed");
  $("sent").hidden = true;
  $("note").value = "";
  $("pick-title").textContent = pick.title;
  $("pick-meta").textContent = `${pick.from}  ·  ${pick.time}  ·  вариантов: ${pick.items.length}`;
  const tab = $("pick-tab");
  tab.hidden = false;
  tab.textContent = `Выбор: ${pick.title}`;
  columns = defaultColumns(pick.items.length);
  const grid = $("grid");
  grid.replaceChildren();
  tiles = pick.items.map((item) => buildTile(item));
  for (const tile of tiles) grid.append(tile.element);
  for (const tile of tiles) tile.viewport.show(tile.item.frames, pick.delay);
  applyColumns();
  renderMarked();
  showPane("pick");
}

function defaultColumns(count) {
  const box = $("grid").getBoundingClientRect();
  const aspect = (box.width || innerWidth) / (box.height || innerHeight - 140);
  let best = 1, bestScore = -1;
  for (let c = 1; c <= count; c++) {
    const rows = Math.ceil(count / c);
    const cell = Math.min(aspect / c, 1 / rows);   // side of a square cell in units of the grid height
    if (cell > bestScore + 1e-9) { best = c; bestScore = cell; }
  }
  return best;
}

function buildTile(item) {
  const element = document.createElement("div");
  element.className = "tile";
  const head = document.createElement("div");
  head.className = "tile-head";
  const check = document.createElement("input");
  check.type = "checkbox";
  const label = document.createElement("span");
  label.className = "label";
  label.textContent = item.label;
  const text = document.createElement("span");
  text.className = "text";
  text.textContent = item.caption;
  text.title = item.caption;
  const choose = document.createElement("button");
  choose.textContent = "Выбрать";
  head.append(check, label, text, choose);
  const port = viewportElement();
  const foot = document.createElement("div");
  foot.className = "tile-foot";
  // Marks of left to right order keep the slashes in place when the start of the path is cut (direction: rtl).
  foot.textContent = `\u200E${item.path}${item.frames.length > 1 ? `  (${item.frames.length} кадров)` : ""}\u200E`;
  foot.title = item.path;
  element.append(head, port, foot);

  const tile = { item, element, check, viewport: null };
  tile.viewport = new Viewport(port, {
    onChange: () => syncedChange(tile),
    onClick: () => toggleMark(tile),
    onDoubleClick: () => openDetail(tiles.indexOf(tile)),
  });
  tile.viewport.view = $("sync").checked ? shared : { ...shared };
  check.addEventListener("change", () => renderMarked());
  label.addEventListener("click", () => toggleMark(tile));
  choose.addEventListener("click", () => submit([item.label]));
  return tile;
}

function syncedChange(tile) {
  if ($("sync").checked) for (const t of tiles) t.viewport.layout();
  else tile.viewport.layout();
}

$("sync").addEventListener("change", () => {
  const on = $("sync").checked;
  for (const tile of tiles) tile.viewport.view = on ? shared : { ...shared };
  for (const tile of tiles) tile.viewport.layout();
});

function applyColumns() {
  const count = tiles.length;
  columns = clamp(columns, 1, Math.max(1, count));
  const rows = Math.ceil(count / columns);
  const grid = $("grid");
  grid.style.gridTemplateColumns = `repeat(${columns}, minmax(0, 1fr))`;
  grid.style.gridAutoRows = `minmax(160px, calc((100% - ${(rows - 1) * 6}px) / ${rows}))`;
  $("cols-value").textContent = columns;
}
$("cols-less").addEventListener("click", () => { columns--; applyColumns(); });
$("cols-more").addEventListener("click", () => { columns++; applyColumns(); });
$("grid").addEventListener("wheel", (e) => {
  // Ctrl + wheel over the grid changes the size of the tiles instead of the zoom.
  if (!e.ctrlKey) return;
  e.preventDefault();
  e.stopPropagation();
  columns += e.deltaY > 0 ? 1 : -1;
  applyColumns();
}, { capture: true, passive: false });

function eachZoomTarget(action) {
  if ($("sync").checked) { action(tiles[0].viewport); return; }
  for (const tile of tiles) action(tile.viewport);
}
$("zoom-fit").addEventListener("click", () => eachZoomTarget((v) => v.reset()));
$("zoom-pixel").addEventListener("click", () => eachZoomTarget((v) => v.pixel()));

function toggleMark(tile) {
  if (pick.closed) { flashClosed(); return; }
  tile.check.checked = !tile.check.checked;
  renderMarked();
}

function markedLabels() { return tiles.filter((t) => t.check.checked).map((t) => t.item.label); }

function renderMarked() {
  for (const tile of tiles) tile.element.classList.toggle("marked", tile.check.checked);
  const marked = markedLabels();
  $("marked").textContent = `Отмечено: ${marked.length ? marked.join(", ") : "—"}`;
  $("submit").disabled = marked.length === 0;
  if (detailIndex >= 0) $("detail-check").checked = tiles[detailIndex].check.checked;
}

function submit(labels) {
  if (!pick || pick.closed || labels.length === 0) return;
  post({ type: "pick-result", id: pick.id, selected: labels, note: $("note").value.trim() });
}
$("submit").addEventListener("click", () => submit(markedLabels()));
$("cancel").addEventListener("click", () => { if (pick && !pick.closed) post({ type: "pick-cancel", id: pick.id }); });
$("note").addEventListener("keydown", (e) => {
  if (e.key === "Enter") { e.preventDefault(); submit(markedLabels()); }
  e.stopPropagation();
});

function closePick(id, answer) {
  if (!pick || pick.id !== id) return;
  pick.closed = true;
  closeDetail();
  for (const tile of tiles) tile.check.disabled = true;
  for (const button of document.querySelectorAll(".tile-head button")) button.disabled = true;
  $("pick").classList.add("closed");
  const sent = $("sent");
  sent.hidden = false;
  if (answer.selected) {
    sent.textContent = `Ответ отправлен: ${answer.selected.join(", ")}${answer.note ? ` — «${answer.note}»` : ""}. `
      + "Этот выбор закрыт, следующий откроется здесь же.";
  } else {
    const reasons = {
      "the user cancelled the pick": "отменён",
      "replaced by a newer pick": "пришёл новый выбор",
      "the caller went away": "Claude больше не ждёт ответа",
    };
    sent.textContent = `Этот выбор закрыт (${reasons[answer.cancelled] ?? answer.cancelled}), ответ на него уже не ждут: `
      + "отметить здесь ничего нельзя. Следующий выбор откроется здесь же.";
  }
  $("pick-tab").textContent = `Выбор: ${pick.title} (закрыт)`;
}

function flashClosed() {
  const sent = $("sent");
  sent.classList.remove("flash");
  void sent.offsetWidth;
  sent.classList.add("flash");
}

// ------------------------------------------------------------------------------------------- one variant, large

let detailIndex = -1;
const detail = new Viewport($("detail-port"), {
  onChange: () => detail.layout(),
  onClick: () => { toggleMark(tiles[detailIndex]); },
  onDoubleClick: () => closeDetail(),
});

function openDetail(index) {
  if (!tiles.length) return;
  detailIndex = (index + tiles.length) % tiles.length;
  const tile = tiles[detailIndex];
  $("detail").hidden = false;
  $("detail-label").textContent = tile.item.label;
  $("detail-caption").textContent = tile.item.caption;
  $("detail-meta").textContent = `${tile.item.path}  ·  ${detailIndex + 1}/${tiles.length}  ·  ←/→ — соседние`;
  $("detail-check").checked = tile.check.checked;
  $("detail-check").disabled = pick.closed;
  $("detail-choose").disabled = pick.closed;
  detail.reset();
  detail.show(tile.item.frames, pick.delay);
}

function closeDetail() {
  detailIndex = -1;
  detail.stop();
  $("detail").hidden = true;
}
$("detail-close").addEventListener("click", closeDetail);
$("detail-check").addEventListener("change", () => { toggleMark(tiles[detailIndex]); });
$("detail-choose").addEventListener("click", () => submit([tiles[detailIndex].item.label]));

