// Buffer check (window.__b): keeps a shadow copy of every ARRAY / ELEMENT_ARRAY buffer from bufferData /
// bufferSubData and, at 10, 18, 24, 30, 40 s, reads each written buffer back with getBufferSubData and compares it
// with the shadow: __b.res.t<s> = {n, same, diff, bytes, first: [[target, size, writes, diff bytes, first index]], err}.
// A diff means the driver lost or changed vertex data.
(function(){
  if (window.__b) return 'already';
  var c = document.querySelector('canvas'); var gl = c.getContext('webgl2');
  var B = window.__b = {sh: new Map(), res: {}, sizes: {}};
  var cur = {}; var ob = gl.bindBuffer; gl.bindBuffer = function(t, b){ cur[t] = b; return ob.apply(gl, arguments); };
  function shadow(t){ var b = cur[t]; if (!b) return null; var s = B.sh.get(b); return s; }
  var od = gl.bufferData; gl.bufferData = function(t, x, u, off, len){ var b = cur[t];
    if ((t === gl.ARRAY_BUFFER || t === gl.ELEMENT_ARRAY_BUFFER) && b) { var size = typeof x === 'number' ? x : (len || x.byteLength); B.sh.set(b, {t: t, data: new Uint8Array(size), size: size, writes: 0}); }
    return od.apply(gl, arguments); };
  var os = gl.bufferSubData; gl.bufferSubData = function(t, dst, src, off, len){ var s = shadow(t);
    if (s && ArrayBuffer.isView(src)) { var u = new Uint8Array(src.buffer, src.byteOffset + (off||0), len || (src.byteLength - (off||0))); s.data.set(u, dst); s.writes++; }
    return os.apply(gl, arguments); };
  B.check = function(name){ var out = {n: 0, same: 0, diff: 0, bytes: 0, first: []};
    B.sh.forEach(function(s, b){ if (!s.writes) return; var tgt = gl.COPY_READ_BUFFER; var prev = gl.getParameter(gl.COPY_READ_BUFFER_BINDING);
      ob.call(gl, tgt, b); var r = new Uint8Array(s.size); gl.getBufferSubData(tgt, 0, r); ob.call(gl, tgt, prev);
      out.n++; var d = 0, fi = -1; for (var i = 0; i < r.length; i++) if (r[i] !== s.data[i]) { d++; if (fi < 0) fi = i; }
      if (d) { out.diff++; out.bytes += d; if (out.first.length < 8) out.first.push([s.t, s.size, s.writes, d, fi]); } else out.same++; });
    out.err = gl.getError(); B.res[name] = out; };
  [10, 18, 24, 30, 40].forEach(function(s){ setTimeout(function(){ B.check('t' + s); }, s*1000); });
  return 'ok';
})()
