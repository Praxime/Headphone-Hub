/* HeadphoneHub recording muxer.
   Takes the raw video-only WebM/Matroska that MediaRecorder produces plus MP3 audio encoded by the page,
   and writes a proper, seekable MP4 (H.264 + MP3) or MKV with a real index (Cues / moov) and duration.
   Nothing is re-encoded: video frames are copied as they are. */
var HHMux = (function () {
  'use strict';

  function enc(s) { var a = []; for (var i = 0; i < s.length; i++) a.push(s.charCodeAt(i) & 255); return a; }
  function cat(list) {
    var n = 0, i;
    for (i = 0; i < list.length; i++) n += list[i].length;
    var out = new Uint8Array(n), o = 0;
    for (i = 0; i < list.length; i++) { out.set(list[i], o); o += list[i].length; }
    return out;
  }
  function be32(n) { return [(n >>> 24) & 255, (n >>> 16) & 255, (n >>> 8) & 255, n & 255]; }
  function be16(n) { return [(n >>> 8) & 255, n & 255]; }
  function u32list(a) {
    var u = new Uint8Array(a.length * 4), dv = new DataView(u.buffer);
    for (var i = 0; i < a.length; i++) dv.setUint32(i * 4, a[i] >>> 0, false);
    return u;
  }

  // ---------------------------------------------------------------- EBML reading

  function ebmlId(u, p) {
    var b = u[p], n = b >= 0x80 ? 1 : b >= 0x40 ? 2 : b >= 0x20 ? 3 : 4, id = 0;
    for (var i = 0; i < n; i++) id = id * 256 + u[p + i];
    return { id: id, n: n };
  }
  function ebmlSize(u, p) {
    var b = u[p], n = 1, mask = 0x80;
    while (n <= 8 && !(b & mask)) { n++; mask >>= 1; }
    if (n > 8) return null;
    var v = b & (mask - 1), unk = v === mask - 1;
    for (var i = 1; i < n; i++) { v = v * 256 + u[p + i]; if (u[p + i] !== 0xFF) unk = false; }
    return { size: unk ? -1 : v, n: n };
  }
  function readUint(u) { var v = 0; for (var i = 0; i < u.length; i++) v = v * 256 + u[i]; return v; }

  // Reads a Blob through a sliding window so a long recording is never fully in memory.
  function Reader(blob) { this.blob = blob; this.size = blob.size; this.win = null; this.base = 0; }
  Reader.prototype.get = async function (p, n) {
    n = Math.max(0, Math.min(n, this.size - p));
    if (!this.win || p < this.base || p + n > this.base + this.win.length) {
      var len = Math.min(this.size - p, Math.max(n, 4194304));
      this.win = new Uint8Array(await this.blob.slice(p, p + len).arrayBuffer());
      this.base = p;
    }
    return this.win.subarray(p - this.base, p - this.base + n);
  };

  var MASTERS = { 0x18538067: 1, 0x1F43B675: 1, 0x1654AE6B: 1, 0xAE: 1, 0xA0: 1, 0x1549A966: 1 };

  // Finds every video frame (offset, size, time, keyframe) without copying the frame data.
  async function parseMatroska(blob) {
    var rd = new Reader(blob), p = 0, size = blob.size;
    var scale = 1000000, clusterTc = 0, codecId = '', priv = null, trackNo = -1, haveTrack = false;
    var frames = [], groupFrame = null;
    while (p < size - 2) {
      var h = await rd.get(p, 12);
      if (h.length < 2) break;
      var a = ebmlId(h, 0);
      if (h.length < a.n + 1) break;
      var s = ebmlSize(h, a.n);
      if (!s) break;
      var ds = p + a.n + s.n, id = a.id;
      if (MASTERS[id]) { if (id === 0xA0) groupFrame = null; p = ds; continue; }
      if (s.size < 0) break;
      var len = s.size;
      if (ds + len > size) break;                       // truncated last element
      if (id === 0xE7) clusterTc = readUint(await rd.get(ds, len));
      else if (id === 0x2AD7B1) scale = readUint(await rd.get(ds, len)) || 1000000;
      else if (id === 0xD7 && !haveTrack) trackNo = readUint(await rd.get(ds, len));
      else if (id === 0x86 && !codecId) codecId = String.fromCharCode.apply(null, await rd.get(ds, len));
      else if (id === 0x63A2 && !priv) { priv = new Uint8Array(await rd.get(ds, len)); haveTrack = true; }
      else if (id === 0xFB) { if (groupFrame) groupFrame.key = false; }
      else if (id === 0xA3 || id === 0xA1) {
        var b = await rd.get(ds, 12);
        var t = ebmlSize(b, 0);
        if (t && len > t.n + 3) {
          var rel = (b[t.n] << 8) | b[t.n + 1];
          if (rel > 32767) rel -= 65536;
          var f = {
            off: ds + t.n + 3, len: len - t.n - 3,
            ts: (clusterTc + rel) * scale / 1000000,
            key: id === 0xA3 ? !!(b[t.n + 2] & 0x80) : true
          };
          frames.push(f);
          groupFrame = id === 0xA1 ? f : null;
        }
      }
      p = ds + len;
    }
    if (!frames.length) throw new Error('no video frames');
    return { rd: rd, blob: blob, codecId: codecId, priv: priv, frames: frames };
  }

  // ---------------------------------------------------------------- H.264 helpers

  function annexbUnits(u) {
    var out = [], i = 0, start = -1, n = u.length;
    while (i + 2 < n) {
      if (u[i] === 0 && u[i + 1] === 0 && u[i + 2] === 1) {
        if (start >= 0) out.push(u.subarray(start, trim(u, start, i)));
        start = i + 3; i += 3;
      } else i++;
    }
    if (start >= 0 && start < n) out.push(u.subarray(start, n));
    return out;
  }
  function trim(u, start, end) { while (end > start && u[end - 1] === 0) end--; return end; }

  function makeAvcc(sps, pps) {
    return cat([[1, sps[1], sps[2], sps[3], 0xFF, 0xE1], be16(sps.length), sps, [1], be16(pps.length), pps]);
  }

  // Turns the recorded frames into length-prefixed (MP4/MKV style) H.264 whatever the browser wrote.
  async function prepareAvc(parsed) {
    var rd = parsed.rd, frames = parsed.frames, priv = parsed.priv;
    var first = await rd.get(frames[0].off, 8);
    var annexb = (first[0] === 0 && first[1] === 0 && (first[2] === 1 || (first[2] === 0 && first[3] === 1)));
    var hasAvcc = priv && priv.length > 7 && priv[0] === 1;
    if (!annexb && !hasAvcc) throw new Error('no avcC');
    if (!annexb) {
      return { avcc: priv, frames: frames.map(function (f) { return { ts: f.ts, key: f.key, len: f.len, part: parsed.blob.slice(f.off, f.off + f.len) }; }) };
    }
    var sps = null, pps = null, out = [];
    for (var i = 0; i < frames.length; i++) {
      var f = frames[i], u = await rd.get(f.off, f.len), units = annexbUnits(u), keep = [], key = false, total = 0;
      for (var k = 0; k < units.length; k++) {
        var nal = units[k], type = nal[0] & 31;
        if (type === 7) { if (!sps) sps = new Uint8Array(nal); continue; }
        if (type === 8) { if (!pps) pps = new Uint8Array(nal); continue; }
        if (type === 9 || type === 12) continue;
        if (type === 5) key = true;
        keep.push(nal); total += 4 + nal.length;
      }
      var d = new Uint8Array(total), o = 0;
      for (k = 0; k < keep.length; k++) { d.set(be32(keep[k].length), o); d.set(keep[k], o + 4); o += 4 + keep[k].length; }
      out.push({ ts: f.ts, key: key, len: d.length, part: d });
    }
    if (!sps || !pps) { if (hasAvcc) return { avcc: priv, frames: out }; throw new Error('no sps/pps'); }
    return { avcc: makeAvcc(sps, pps), frames: out };
  }

  // Sorts out times: starts at zero, strictly increasing, with a duration for every frame.
  function finishTimes(frames) {
    var t0 = frames[0].ts, i, prev = -1;
    for (i = 0; i < frames.length; i++) {
      var t = Math.round(frames[i].ts - t0);
      if (t <= prev) t = prev + 1;
      frames[i].ts = t; prev = t;
    }
    var ds = [];
    for (i = 0; i < frames.length - 1; i++) { frames[i].dur = frames[i + 1].ts - frames[i].ts; ds.push(frames[i].dur); }
    ds.sort(function (a, b) { return a - b; });
    var typical = ds.length ? ds[ds.length >> 1] : 33;
    frames[frames.length - 1].dur = Math.max(1, typical);
    if (!frames.some(function (f) { return f.key; })) frames[0].key = true;
    return frames[frames.length - 1].ts + frames[frames.length - 1].dur;
  }

  // Returns { kind:'avc'|'other', codecId, priv, frames, durMs }
  async function prepareVideo(parsed) {
    var id = parsed.codecId, v;
    if (id === 'V_MPEG4/ISO/AVC' || id === '') {
      v = await prepareAvc(parsed);
      v = { kind: 'avc', codecId: 'V_MPEG4/ISO/AVC', priv: v.avcc, frames: v.frames };
    } else {
      v = {
        kind: 'other', codecId: id, priv: parsed.priv,
        frames: parsed.frames.map(function (f) { return { ts: f.ts, key: f.key, len: f.len, part: parsed.blob.slice(f.off, f.off + f.len) }; })
      };
    }
    v.durMs = finishTimes(v.frames);
    return v;
  }

  // ---------------------------------------------------------------- MP3 (from lamejs)

  var BR = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0], SRT = [44100, 48000, 32000, 0];

  function splitMp3(u) {
    var p = 0, frames = [], rate = 0;
    while (p + 4 <= u.length) {
      if (u[p] === 0xFF && (u[p + 1] & 0xE0) === 0xE0) {
        var ver = (u[p + 1] >> 3) & 3, layer = (u[p + 1] >> 1) & 3, br = BR[u[p + 2] >> 4], sr = SRT[(u[p + 2] >> 2) & 3], pad = (u[p + 2] >> 1) & 1;
        if (ver === 3 && layer === 1 && br && sr) {
          var len = Math.floor(144000 * br / sr) + pad;
          if (p + len > u.length) break;
          var fr = u.subarray(p, p + len), tag = String.fromCharCode.apply(null, fr.subarray(36, 40));
          if (!(frames.length === 0 && (tag === 'Info' || tag === 'Xing'))) frames.push(fr);
          rate = sr; p += len; continue;
        }
      }
      p++;
    }
    return { frames: frames, rate: rate };
  }

  // Incremental MP3 encoder: put() takes interleaved 16-bit stereo, finish() returns the file bytes.
  function Mp3Encoder(rate, kbps) {
    var e = new lamejs.Mp3Encoder(2, rate, kbps), out = [], size = 0;
    function keep(b) { if (b && b.length) { var c = new Uint8Array(b.length); c.set(b); out.push(c); size += c.length; } }
    this.put = function (pcm) {
      var n = pcm.length >> 1, l = new Int16Array(n), r = new Int16Array(n);
      for (var i = 0, j = 0; i < n; i++, j += 2) { l[i] = pcm[j]; r[i] = pcm[j + 1]; }
      for (var k = 0; k < n; k += 4608) keep(e.encodeBuffer(l.subarray(k, k + 4608), r.subarray(k, k + 4608)));
    };
    this.finish = function () { keep(e.flush()); return cat(out); };
    this.bytes = function () { return size; };
  }

  // ---------------------------------------------------------------- MP4

  function box(type, parts) {
    var body = cat(parts);
    return cat([be32(8 + body.length), enc(type), body]);
  }
  function fullbox(type, ver, parts) { return box(type, [[ver, 0, 0, 0]].concat(parts)); }

  var MATRIX = [0x00010000, 0, 0, 0, 0x00010000, 0, 0, 0, 0x40000000];

  function runs(vals) {
    var out = [], i = 0;
    while (i < vals.length) { var j = i; while (j < vals.length && vals[j] === vals[i]) j++; out.push(j - i, vals[i]); i = j; }
    return out;
  }
  function stscTable(counts) {
    var e = [], prev = -1;
    for (var i = 0; i < counts.length; i++) if (counts[i] !== prev) { e.push(i + 1, counts[i], 1); prev = counts[i]; }
    return e;
  }

  function trak(id, isVideo, mediaScale, mediaDur, movieDurMs, w, h, entry, stts, stss, chunkCounts, sizes, offsets) {
    var tkhd = box('tkhd', [[0, 0, 0, 3], be32(0), be32(0), be32(id), be32(0), be32(movieDurMs), be32(0), be32(0),
      be16(0), be16(0), be16(isVideo ? 0 : 0x0100), be16(0), u32list(MATRIX), be32(isVideo ? w * 65536 : 0), be32(isVideo ? h * 65536 : 0)]);
    var mdhd = fullbox('mdhd', 0, [be32(0), be32(0), be32(mediaScale), be32(mediaDur), be16(0x55C4), be16(0)]);
    var hdlr = fullbox('hdlr', 0, [be32(0), enc(isVideo ? 'vide' : 'soun'), be32(0), be32(0), be32(0), enc(isVideo ? 'VideoHandler\0' : 'SoundHandler\0')]);
    var mhd = isVideo ? box('vmhd', [[0, 0, 0, 1], be16(0), be16(0), be16(0), be16(0)]) : fullbox('smhd', 0, [be16(0), be16(0)]);
    var dinf = box('dinf', [fullbox('dref', 0, [be32(1), box('url ', [[0, 0, 0, 1]])])]);
    var stbl = [fullbox('stsd', 0, [be32(1), entry]),
      fullbox('stts', 0, [be32(stts.length / 2), u32list(stts)])];
    if (stss) stbl.push(fullbox('stss', 0, [be32(stss.length), u32list(stss)]));
    var sc = stscTable(chunkCounts);
    stbl.push(fullbox('stsc', 0, [be32(sc.length / 3), u32list(sc)]),
      fullbox('stsz', 0, [be32(0), be32(sizes.length), u32list(sizes)]),
      fullbox('stco', 0, [be32(offsets.length), u32list(offsets)]));
    var minf = box('minf', [mhd, dinf, box('stbl', stbl)]);
    return box('trak', [tkhd, box('mdia', [mdhd, hdlr, minf])]);
  }

  function avc1Entry(avcc, w, h) {
    return box('avc1', [[0, 0, 0, 0, 0, 0], be16(1), be16(0), be16(0), be32(0), be32(0), be32(0), be16(w), be16(h),
      be32(0x00480000), be32(0x00480000), be32(0), be16(1), new Uint8Array(32), be16(0x18), be16(0xFFFF), box('avcC', [avcc])]);
  }
  function mp3Entry(rate) {
    var dec = [0x04, 13, 0x6B, 0x15, 0, 0x10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    var esd = [0x03, 3 + dec.length + 3, 0, 2, 0].concat(dec, [0x06, 1, 0x02]);
    return box('mp4a', [[0, 0, 0, 0, 0, 0], be16(1), be32(0), be32(0), be16(2), be16(16), be16(0), be16(0), be32(rate * 65536),
      fullbox('esds', 0, [esd])]);
  }

  function buildMp4(video, audio, W, H) {
    var VS = 90000, vf = video.frames, af = audio ? audio.frames : [], arate = audio ? audio.rate : 44100;
    var vDur = video.durMs, aDur = audio ? Math.round(af.length * 1152 * 1000 / arate) : 0;
    var movieDur = Math.max(vDur, aDur), i;

    // one chunk per second per track, interleaved
    var vch = [], ach = [], cur = null;
    for (i = 0; i < vf.length; i++) {
      var sec = Math.floor(vf[i].ts / 1000);
      if (!cur || cur.sec !== sec) { cur = { sec: sec, from: i, n: 0 }; vch.push(cur); }
      cur.n++;
    }
    cur = null;
    for (i = 0; i < af.length; i++) {
      var s2 = Math.floor(i * 1152 / arate);
      if (!cur || cur.sec !== s2) { cur = { sec: s2, from: i, n: 0 }; ach.push(cur); }
      cur.n++;
    }
    var order = [], a = 0, b = 0;
    while (a < vch.length || b < ach.length) {
      if (b >= ach.length || (a < vch.length && vch[a].sec <= ach[b].sec)) order.push({ v: true, c: vch[a++] });
      else order.push({ v: false, c: ach[b++] });
    }

    var vSizes = vf.map(function (f) { return f.len; }), aSizes = af.map(function (f) { return f.length; });
    var vStts = [], d = [];
    for (i = 0; i < vf.length; i++) d.push(vf[i].dur * 90);
    vStts = runs(d);
    var stss = [];
    for (i = 0; i < vf.length; i++) if (vf[i].key) stss.push(i + 1);
    var vCounts = vch.map(function (c) { return c.n; }), aCounts = ach.map(function (c) { return c.n; });

    function moov(vOff, aOff) {
      var traks = [trak(1, true, VS, Math.round(vDur * 90), movieDur, W, H, avc1Entry(video.priv, W, H), vStts, stss.length === vf.length ? null : stss, vCounts, vSizes, vOff)];
      if (audio) traks.push(trak(2, false, arate, af.length * 1152, movieDur, 0, 0, mp3Entry(arate), [af.length, 1152], null, aCounts, aSizes, aOff));
      var mvhd = fullbox('mvhd', 0, [be32(0), be32(0), be32(1000), be32(movieDur), be32(0x00010000), be16(0x0100), be16(0),
        be32(0), be32(0), u32list(MATRIX), new Uint8Array(24), be32(audio ? 3 : 2)]);
      return box('moov', [mvhd].concat(traks));
    }
    var ftyp = box('ftyp', [enc('isom'), be32(512), enc('isom'), enc('iso2'), enc('avc1'), enc('mp41')]);
    var dummyV = vch.map(function () { return 0; }), dummyA = ach.map(function () { return 0; });
    var moovLen = moov(dummyV, dummyA).length;
    var base = ftyp.length + moovLen + 8, run = 0, vOff = [], aOff = [], payload = [];
    for (i = 0; i < order.length; i++) {
      var o = order[i], c = o.c, k, size = 0;
      if (o.v) {
        vOff.push(base + run);
        for (k = c.from; k < c.from + c.n; k++) { payload.push(vf[k].part); size += vf[k].len; }
      } else {
        aOff.push(base + run);
        var chunk = cat(af.slice(c.from, c.from + c.n));
        payload.push(chunk); size = chunk.length;
      }
      run += size;
    }
    if (run + 8 > 4294967295) throw new Error('too large');
    var mdat = cat([be32(run + 8), enc('mdat')]);
    return new Blob([ftyp, moov(vOff, aOff), mdat].concat(payload), { type: 'video/mp4' });
  }

  // ---------------------------------------------------------------- MKV

  function idBytes(id) { var b = []; while (id > 0) { b.unshift(id & 255); id = Math.floor(id / 256); } return b; }
  function sizeBytes(n, fixed) {
    var len = fixed || 1;
    while (len < 8 && n >= Math.pow(2, 7 * len) - 1) len++;
    var out = new Array(len), v = n;
    for (var i = len - 1; i >= 0; i--) { out[i] = v % 256; v = Math.floor(v / 256); }
    out[0] |= (0x80 >> (len - 1));
    return out;
  }
  function el(id, payload) { return cat([idBytes(id), sizeBytes(payload.length), payload]); }
  function uintBytes(n, fixed) {
    var b = [];
    while (n > 0) { b.unshift(n % 256); n = Math.floor(n / 256); }
    if (!b.length) b.push(0);
    while (fixed && b.length < fixed) b.unshift(0);
    return b;
  }
  function elU(id, n, fixed) { return el(id, uintBytes(n, fixed)); }
  function elS(id, s) { return el(id, enc(s)); }
  function elF(id, x) { var u = new Uint8Array(8); new DataView(u.buffer).setFloat64(0, x, false); return el(id, u); }

  function buildMkv(video, audio, W, H) {
    var vf = video.frames, af = audio ? audio.frames : [], arate = audio ? audio.rate : 44100, i;
    var aTs = af.map(function (_, n) { return Math.round(n * 1152 * 1000 / arate); });
    var durMs = Math.max(video.durMs, af.length ? aTs[af.length - 1] + Math.round(1152 * 1000 / arate) : 0);

    var info = el(0x1549A966, cat([elU(0x2AD7B1, 1000000), elS(0x4D80, 'HeadphoneHub'), elS(0x5741, 'HeadphoneHub'), elF(0x4489, durMs)]));
    var vTrack = el(0xAE, cat([elU(0xD7, 1), elU(0x73C5, 1), elU(0x83, 1), elU(0x9C, 0), elS(0x86, video.codecId),
      video.priv && video.priv.length ? el(0x63A2, video.priv) : new Uint8Array(0),
      el(0xE0, cat([elU(0xB0, W), elU(0xBA, H)]))]));
    var tracks = [vTrack];
    if (audio) tracks.push(el(0xAE, cat([elU(0xD7, 2), elU(0x73C5, 2), elU(0x83, 2), elU(0x9C, 0), elS(0x86, 'A_MPEG/L3'),
      el(0xE1, cat([elF(0xB5, arate), elU(0x9F, 2)]))])));
    tracks = el(0x1654AE6B, cat(tracks));

    // blocks in time order (video first on ties), grouped into clusters that start on keyframes
    var blocks = [], a = 0, b = 0;
    while (a < vf.length || b < af.length) {
      if (b >= af.length || (a < vf.length && vf[a].ts <= aTs[b])) { blocks.push({ ts: vf[a].ts, key: vf[a].key, v: true, len: vf[a].len, part: vf[a].part }); a++; }
      else { blocks.push({ ts: aTs[b], key: true, v: false, len: af[b].length, part: af[b] }); b++; }
    }
    var clusters = [], cl = null;
    for (i = 0; i < blocks.length; i++) {
      var bk = blocks[i];
      if (!cl || (bk.v && bk.key && bk.ts - cl.tc >= 1000) || bk.ts - cl.tc > 10000) {
        cl = { tc: bk.ts, items: [], cue: false };
        clusters.push(cl);
      }
      if (bk.v && bk.key) cl.cue = true;
      cl.items.push(bk);
    }
    var cparts = [], offsets = [], run = 0;
    for (i = 0; i < clusters.length; i++) {
      var c = clusters[i], inner = [], size = 0, tcEl = elU(0xE7, c.tc);
      inner.push(tcEl); size += tcEl.length;
      for (var k = 0; k < c.items.length; k++) {
        var it = c.items[k], rel = it.ts - c.tc;
        var hd = cat([[0xA3], sizeBytes(it.len + 4), [it.v ? 0x81 : 0x82], be16(rel), [it.key ? 0x80 : 0x00]]);
        inner.push(hd, it.part); size += hd.length + it.len;
      }
      var head = cat([idBytes(0x1F43B675), sizeBytes(size)]);
      offsets.push(run);
      cparts.push(head); cparts = cparts.concat(inner);
      run += head.length + size;
    }

    function cues(base) {
      var pts = [];
      for (var n = 0; n < clusters.length; n++) if (clusters[n].cue)
        pts.push(el(0xBB, cat([elU(0xB3, clusters[n].tc, 4), el(0xB7, cat([elU(0xF7, 1), elU(0xF1, base + offsets[n], 4)]))])));
      return pts.length ? el(0x1C53BB6B, cat(pts)) : new Uint8Array(0);
    }
    function seekhead(pi, pt, pc) {
      function seek(id, pos) { return el(0x4DBB, cat([el(0x53AB, id), elU(0x53AC, pos, 4)])); }
      var list = [seek(idBytes(0x1549A966), pi), seek(idBytes(0x1654AE6B), pt)];
      if (pc >= 0) list.push(seek(idBytes(0x1C53BB6B), pc));
      return el(0x114D9B74, cat(list));
    }
    var cuesLen = cues(0).length;
    var shLen = seekhead(0, 0, cuesLen ? 0 : -1).length;
    var pInfo = shLen, pTracks = pInfo + info.length, pCues = pTracks + tracks.length, base = pCues + cuesLen;
    var sh = seekhead(pInfo, pTracks, cuesLen ? pCues : -1), cu = cues(base);
    var segSize = base + run;
    var ebml = el(0x1A45DFA3, cat([elU(0x4286, 1), elU(0x42F7, 1), elU(0x42F2, 4), elU(0x42F3, 8), elS(0x4282, 'matroska'), elU(0x4287, 4), elU(0x4285, 2)]));
    var seg = cat([idBytes(0x18538067), sizeBytes(segSize, 8)]);
    return new Blob([ebml, seg, sh, info, tracks, cu].concat(cparts), { type: 'video/x-matroska' });
  }

  return {
    parse: parseMatroska,
    prepareVideo: prepareVideo,
    splitMp3: splitMp3,
    Mp3Encoder: Mp3Encoder,
    mp4: buildMp4,
    mkv: buildMkv
  };
})();
