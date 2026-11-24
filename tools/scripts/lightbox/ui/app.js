"use strict";

// The tabs, the viewer of the shown images, the keyboard and the messages of the window (window.py).

// ------------------------------------------------------------------------------------------------------------ tabs

let activePane = "view";
function showPane(name) {
  activePane = name;
  for (const tab of document.querySelectorAll(".tab")) tab.classList.toggle("active", tab.dataset.pane === name);
  for (const pane of document.querySelectorAll(".pane")) pane.classList.toggle("active", pane.id === name);
  if (name === "pick") $("pick-tab").classList.remove("attention");
}
for (const tab of document.querySelectorAll(".tab")) tab.addEventListener("click", () => showPane(tab.dataset.pane));

// ------------------------------------------------------------------------------------------------------- the viewer

const history = [];
let current = -1;
const viewer = new Viewport($("view-port"), {
  onChange: () => viewer.layout(),
  onDoubleClick: () => (viewer.view.k === 1 ? viewer.pixel() : viewer.reset()),
  onSize: (size) => { if (current >= 0) history[current].size = size; renderViewMeta(); },
});

function addShown(items, select) {
  for (const item of items) {
    history.push(item);
    const thumb = document.createElement("img");
    thumb.src = item.src;
    thumb.title = `${item.from}\n${item.path}`;
    const index = history.length - 1;
    thumb.addEventListener("click", () => selectShown(index));
    $("view-strip").append(thumb);
  }
  $("view-count").textContent = history.length;
  if (select && items.length) selectShown(history.length - 1);
}

function selectShown(index) {
  if (index < 0 || index >= history.length) return;
  current = index;
  const item = history[index];
  viewer.reset();
  viewer.show([item.src], 0);
  const thumbs = $("view-strip").children;
  for (let i = 0; i < thumbs.length; i++) thumbs[i].classList.toggle("current", i === index);
  thumbs[index].scrollIntoView({ inline: "nearest", block: "nearest" });
  renderViewMeta();
}

function renderViewMeta() {
  const item = history[current];
  $("view-from").textContent = item.from;
  const size = item.size ? `  ·  ${item.size.w}×${item.size.h}` : "";
  $("view-meta").textContent = `${item.path}  ·  ${item.time}${size}  ·  ${current + 1}/${history.length}`;
}

// ----------------------------------------------------------------------------------------------------- keyboard

function zoomKeys(e, targets) {
  if (e.key === "0") targets((v) => v.reset());
  else if (e.key === "1") targets((v) => v.pixel());
  else if (e.key === "+" || e.key === "=") targets((v) => v.zoomCenter(1.25));
  else if (e.key === "-") targets((v) => v.zoomCenter(0.8));
  else return false;
  return true;
}

document.addEventListener("keydown", (e) => {
  if (e.ctrlKey || e.altKey || e.metaKey) return;
  if (detailIndex >= 0) {
    if (e.key === "Escape") closeDetail();
    else if (e.key === "ArrowLeft") openDetail(detailIndex - 1);
    else if (e.key === "ArrowRight") openDetail(detailIndex + 1);
    else if (e.key === " ") toggleMark(tiles[detailIndex]);
    else if (!zoomKeys(e, (a) => a(detail))) return;
    e.preventDefault();
    return;
  }
  if (activePane === "view") {
    if (e.key === "ArrowLeft") selectShown(current - 1);
    else if (e.key === "ArrowRight") selectShown(current + 1);
    else if (!zoomKeys(e, (a) => a(viewer))) return;
    e.preventDefault();
    return;
  }
  if (!pick) return;
  if (zoomKeys(e, eachZoomTarget)) { e.preventDefault(); return; }
  if (e.key === "Enter") { submit(markedLabels()); e.preventDefault(); return; }
  const tile = tiles.find((t) => t.item.label.toLowerCase() === e.key.toLowerCase());
  if (tile) { toggleMark(tile); e.preventDefault(); }
});

// ------------------------------------------------------------------------------------------------- from python

window.cv = {
  receive(message) {
    switch (message.type) {
      case "history": addShown(message.items, true); break;
      case "shown":
        addShown(message.items, true);
        if (activePane !== "view" && !(pick && !pick.closed)) showPane("view");
        break;
      case "pick": startPick(message.pick); break;
      case "pick-closed": closePick(message.id, message.answer); break;
      default: throw new Error(`unknown message ${message.type}`);
    }
  },
};
post({ type: "ready" });
