// Frame recorder (window.__f): wraps every function of WebGL2RenderingContext.prototype on the context of the first
// canvas and records every GL call of one frame as text (arguments rounded, objects named Texture3 / Program7, typed
// arrays up to 64 values inline, larger slices as a hash), taken at 8, 14, 20, 26, 32, 40, 55 s after the injection
// into __f.frames.t<s>; __f.take(name) records one more. Compare two frames to see whether the command stream changed.
(function(){
  if (window.__f) return 'already';
  var c = document.querySelector('canvas'); var gl = c.getContext('webgl2');
  var F = window.__f = {rec: null, frames: {}};
  var ids = new Map(), n = 1; function id(o){ if(!ids.has(o)) ids.set(o, (o.constructor && o.constructor.name || 'o').replace('WebGL','') + (n++)); return ids.get(o); }
  function fmt(a){ if (a === null || a === undefined) return String(a); if (typeof a === 'number') return Math.round(a*1e4)/1e4; if (typeof a === 'string') return a.slice(0,40); if (typeof a === 'boolean') return a;
    if (ArrayBuffer.isView(a)) { if (a.length > 64) return a.constructor.name + '[' + a.length + ']'; return '[' + Array.prototype.slice.call(a,0,64).map(function(x){return Math.round(x*1e4)/1e4;}).join(',') + ']'; }
    if (typeof a === 'object') return id(a); return typeof a; }
  var proto = WebGL2RenderingContext.prototype;
  Object.getOwnPropertyNames(proto).forEach(function(k){
    var d = Object.getOwnPropertyDescriptor(proto, k); if (!d || typeof d.value !== 'function') return;
    var orig = gl[k];
    gl[k] = function(){ var r = orig.apply(gl, arguments); if (F.rec) { var s = k + '(' + Array.prototype.map.call(arguments, fmt).join(',') + ')'; var A = arguments; for (var i = 0; i < A.length; i++) { if (ArrayBuffer.isView(A[i]) && A[i].length > 64 && typeof A[i+1] === 'number') { var len = A[i+2] || 0; var sl = A[i].subarray(A[i+1], A[i+1] + len); if (len <= 64) s += ' =' + fmt(sl); else { var h = 0; var u = new Uint8Array(sl.buffer, sl.byteOffset, sl.byteLength); for (var j = 0; j < u.length; j++) h = (h * 31 + u[j]) | 0; s += ' #' + h; } } } if (r && typeof r === 'object') s += '->' + id(r); F.rec.push(s); } return r; };
  });
  F.take = function(name){ requestAnimationFrame(function(){ F.rec = []; requestAnimationFrame(function(){ F.frames[name] = F.rec; F.rec = null; }); }); };
  var t0 = performance.now();
  [8, 14, 20, 26, 32, 40, 55].forEach(function(s){ setTimeout(function(){ F.take('t' + s); }, s*1000); });
  return 'ok';
})()
