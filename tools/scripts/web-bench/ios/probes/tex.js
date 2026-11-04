// Texture check (window.__t): keeps the RGBA8 level-0 texSubImage2D uploads (up to 600) and, at 10, 18, 24, 30, 40,
// 55 s, reads each region back through a framebuffer with readPixels: __t.res.t<s> = {n, same, zero, diff, fmts, err}.
// zero / diff mean the texture contents were lost or changed after the upload.
(function(){
  if (window.__t) return 'already';
  var c = document.querySelector('canvas'); var gl = c.getContext('webgl2');
  var T = window.__t = {list: [], res: {}};
  var bound = null;
  var ob = gl.bindTexture; gl.bindTexture = function(t, tex){ if (t === gl.TEXTURE_2D) bound = tex; return ob.apply(gl, arguments); };
  var os = gl.texSubImage2D; gl.texSubImage2D = function(t, lvl, x, y, w, h, fmt, type, src, off){
    if (t === gl.TEXTURE_2D && lvl === 0 && fmt === gl.RGBA && type === gl.UNSIGNED_BYTE && ArrayBuffer.isView(src) && T.list.length < 600)
      T.list.push({tex: bound, w: w, h: h, x: x, y: y, data: new Uint8Array(src.buffer, src.byteOffset + (off||0), w*h*4).slice()});
    return os.apply(gl, arguments);
  };
  T.check = function(name){
    var prev = gl.getParameter(gl.FRAMEBUFFER_BINDING), fb = gl.createFramebuffer(), out = {n: 0, same: 0, zero: 0, diff: 0, fmts: {}};
    gl.bindFramebuffer(gl.FRAMEBUFFER, fb);
    T.list.forEach(function(e){
      gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, e.tex, 0);
      if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) { out.fmts.incomplete = (out.fmts.incomplete||0)+1; return; }
      var px = new Uint8Array(e.w*e.h*4); gl.readPixels(e.x, e.y, e.w, e.h, gl.RGBA, gl.UNSIGNED_BYTE, px);
      out.n++; var nz = 0, eq = true; for (var i = 0; i < px.length; i++) { if (px[i]) nz++; if (px[i] !== e.data[i]) eq = false; }
      if (eq) out.same++; else if (!nz) out.zero++; else out.diff++;
    });
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, null, 0);
    gl.bindFramebuffer(gl.FRAMEBUFFER, prev); gl.deleteFramebuffer(fb);
    out.err = gl.getError(); T.res[name] = out;
  };
  [10, 18, 24, 30, 40, 55].forEach(function(s){ setTimeout(function(){ T.check('t' + s); }, s*1000); });
  return 'ok';
})()
