// General GL probe (window.__p): wraps the WebGL context of the first canvas and records, per second of the page,
// the counts of texture / draw / delete / link calls (__p.sec), the first 400 texture events [ms, kind, texture id,
// width, height] (__p.ev: img, stor, cimg, sub, del, delprog) and context loss / restore (__p.lost); every second it
// also stores isContextLost, getError, the canvas size and the visibility. Inject after the game made its context
// (probe-run.py waits for the ready expression). A hook that throws is swallowed: the probe must not break the game.
// Answers 'ok gl2' / 'ok gl1', or 'nocanvas' / 'nogl' / 'already'.
(function(){
  if (window.__p) return 'already';
  var c = document.querySelector('canvas'); if (!c) return 'nocanvas';
  var gl = c.getContext('webgl2') || c.getContext('webgl'); if (!gl) return 'nogl';
  var t0 = performance.now(), P = window.__p = {ev:[], sec:{}, lost:[]};
  function now(){ return Math.round(performance.now()-t0); }
  function bucket(){ var s = Math.floor((performance.now()-t0)/1000); return P.sec[s] || (P.sec[s] = {}); }
  var texId = new Map(), next = 1;
  function tid(t){ if(!t) return 0; if(!texId.has(t)) texId.set(t, next++); return texId.get(t); }
  var bound = 0;
  function wrap(name, fn){ var orig = gl[name]; gl[name] = function(){ var b = bucket(); b[name]=(b[name]||0)+1; try { fn && fn.apply(null, arguments); } catch(e){} return orig.apply(gl, arguments); }; }
  wrap('bindTexture', function(t, tex){ bound = tid(tex); });
  wrap('deleteTexture', function(tex){ if (P.ev.length < 400) P.ev.push([now(), 'del', tid(tex)]); });
  wrap('createTexture');
  wrap('texImage2D', function(){ var a = arguments; if (P.ev.length < 400) P.ev.push([now(), 'img', bound, a[3], a[4]]); });
  wrap('texStorage2D', function(){ var a = arguments; if (P.ev.length < 400) P.ev.push([now(), 'stor', bound, a[3], a[4]]); });
  wrap('compressedTexImage2D', function(){ var a = arguments; if (P.ev.length < 400) P.ev.push([now(), 'cimg', bound, a[3], a[4]]); });
  wrap('texSubImage2D', function(){ var a = arguments; if (P.ev.length < 400) P.ev.push([now(), 'sub', bound, a[4], a[5]]); });
  wrap('drawElements'); wrap('drawArrays'); wrap('drawElementsInstanced');
  wrap('deleteFramebuffer'); wrap('deleteBuffer'); wrap('deleteProgram', function(){ P.ev.push([now(),'delprog']); });
  wrap('linkProgram');
  setInterval(function(){ var b = bucket(); b.lost = gl.isContextLost() ? 1 : 0; b.err = gl.getError(); b.w = c.width; b.h = c.height; b.vis = document.visibilityState[0]; }, 1000);
  c.addEventListener('webglcontextlost', function(){ P.lost.push(['lost', now()]); });
  c.addEventListener('webglcontextrestored', function(){ P.lost.push(['restored', now()]); });
  return 'ok ' + (gl instanceof WebGL2RenderingContext ? 'gl2' : 'gl1');
})()
