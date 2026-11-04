// Uniform dump (window.__u): remembers every program passed to useProgram and, at 4 ... 66 s, reads all non-sampler
// uniforms of each with getUniform (arrays element by element, values rounded to 1e-4): __u.res.t<s> = [{name: value}]
// per program in the order of their first use; __u.res.err<s> holds an error of a dump. A uniform that cannot be read
// is skipped. Scan the result with uni-scan.py.
// This found the iPadOS 18.2 (Safari, iPad 7 A10) bug: after ~1100-1200 frames (18-22 s at 60 fps, 34-38 s at 30) the
// UI programs get glstate_matrix_projection[3] and unity_MatrixVP[3] = [0,0,0,0] instead of [0,0,0,1] (w = 0, the UI
// vertices collapse and the Screen Space - Overlay UI disappears), while textures, buffers and the GL command stream
// stay the same (tex.js, buf.js, frame.js); likely a tier-up bug of the wasm JIT. Screen Space - Camera canvases avoid it.
(function(){
  if (window.__u) return 'already';
  var c = document.querySelector('canvas'); var gl = c.getContext('webgl2');
  var U = window.__u = {res: {}, progs: new Set()};
  var ou = gl.useProgram; gl.useProgram = function(p){ if (p) U.progs.add(p); return ou.apply(gl, arguments); };
  function r(v){ if (v && v.length !== undefined) return Array.prototype.map.call(v, function(x){ return Math.round(x*1e4)/1e4; }); return v; }
  U.dump = function(name){ var out = []; var i = 0;
    U.progs.forEach(function(p){ i++; var n = gl.getProgramParameter(p, gl.ACTIVE_UNIFORMS), u = {};
      for (var k = 0; k < n; k++) { var a = gl.getActiveUniform(p, k); if (a.type === gl.SAMPLER_2D) continue; var base = a.name.replace(/\[0\]$/, ''); for (var e2 = 0; e2 < a.size; e2++) { var nm = a.size > 1 ? base + '[' + e2 + ']' : a.name; var L = gl.getUniformLocation(p, nm); if (!L) continue; try { u[nm] = r(gl.getUniform(p, L)); } catch (e) {} } }
      out.push(u); });
    U.res[name] = out; };
  [4,8,12,14,16,18,20,22,24,26,30,34,38,42,46,50,55,60,66].forEach(function(s){ setTimeout(function(){ try { U.dump('t' + s); } catch (e) { U.res['err' + s] = String(e); } }, s*1000); });
  return 'ok';
})()
