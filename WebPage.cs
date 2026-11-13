using System.Text;

namespace HeadphoneHub;

internal static class WebPage
{
    public static byte[] Render(string? themeJson) =>
        Encoding.UTF8.GetBytes(Source.Replace("__THEME__", themeJson ?? "null"));

    private const string Source = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<meta name="theme-color" content="#0e0e11">
<title>Headphone Hub</title>
<style>
html{color-scheme:light dark}
:root{--bg:#f2f2f7;--card:#fff;--fg:#000;--sub:#8e8e93;--fill:#e9e9ea;--accent:#007aff;--viz:#e6e6ec}
*{box-sizing:border-box;-webkit-tap-highlight-color:transparent}
html{height:100%}
body{margin:0;height:100vh;height:100dvh}
body{background:var(--bg);color:var(--fg);font-family:system-ui,-apple-system,"Segoe UI",Roboto,Tahoma,sans-serif;display:flex;align-items:center;justify-content:center;overflow:hidden;padding:max(16px,env(safe-area-inset-top)) max(16px,env(safe-area-inset-right)) max(16px,env(safe-area-inset-bottom)) max(16px,env(safe-area-inset-left))}
.wrap{container-type:inline-size;width:min(440px,92vw,72vh)}
@media (pointer:coarse),(max-aspect-ratio:3/5){.wrap{width:min(78vw,72vh)}}
.card{width:100%;background:var(--card);border-radius:6.4cqw;padding:7.5cqw 6.4cqw;text-align:center;border:max(1px,.3cqw) solid var(--fill);border:max(1px,.3cqw) solid color-mix(in srgb,var(--fg) 14%,transparent);box-shadow:0 10px 40px rgba(0,0,0,.14);animation:rise .55s cubic-bezier(.2,.8,.2,1)}
@keyframes rise{from{opacity:0;transform:translateY(18px) scale(.95)}to{opacity:1;transform:none}}
.logo{width:17.5cqw;height:17.5cqw;margin:0 auto 3.6cqw;border-radius:28%;background:var(--accent);display:flex;align-items:center;justify-content:center}
.logo svg{width:52%;height:52%;stroke:#fff;fill:none;stroke-width:1.9;stroke-linecap:round;stroke-linejoin:round}
h1{font-size:6.2cqw;margin:0 0 1.4cqw}
#status{color:var(--sub);font-size:3.3cqw;min-height:4.8cqw;margin:0 0 4.6cqw}
canvas{display:block;width:100%;height:30cqw;margin:0 0 4.6cqw}
button{width:100%;border:0;border-radius:4.1cqw;padding:3.8cqw;font:inherit;font-size:4cqw;font-weight:600;color:#fff;background:var(--accent);cursor:pointer;transition:transform .15s,opacity .3s}
button:active{transform:scale(.96)}
button:disabled{opacity:.45}
button.stop{background:var(--fill);color:var(--fg)}
</style>
</head>
<body>
<div class="wrap"><div class="card">
<div class="logo"><svg viewBox="0 0 24 24"><path d="M3,18 V12 A9,9 0 0 1 21,12 V18 M21,19 A2,2 0 0 1 19,21 H18 A2,2 0 0 1 16,19 V16 A2,2 0 0 1 18,14 H21 Z M3,19 A2,2 0 0 0 5,21 H6 A2,2 0 0 0 8,19 V16 A2,2 0 0 0 6,14 H3 Z"/></svg></div>
<h1 id="title">Headphone Hub</h1>
<p id="status"></p>
<canvas id="viz"></canvas>
<button id="btn" disabled></button>
</div></div>
<script>
(function () {
  var $ = function (id) { return document.getElementById(id); };
  var fa = (navigator.language || '').toLowerCase().indexOf('fa') === 0;
  var text = fa ? {
    connecting: 'در حال اتصال…',
    lost: 'اتصال قطع شد. در حال اتصال دوباره…',
    waiting: 'وصل شد. منتظر صدا از رایانه…',
    ready: 'آماده پخش',
    playing: 'در حال پخش زنده',
    start: 'برای گوش دادن بزنید',
    stop: 'توقف'
  } : {
    connecting: 'Connecting…',
    lost: 'Connection lost. Reconnecting…',
    waiting: 'Connected. Waiting for audio from the PC…',
    ready: 'Ready to listen',
    playing: 'Listening live',
    start: 'Tap to listen',
    stop: 'Stop'
  };

  if (fa) {
    document.documentElement.lang = 'fa';
    document.documentElement.dir = 'rtl';
    $('title').textContent = 'هدفون هاب';
  }
  if (navigator.audioSession) navigator.audioSession.type = 'playback';

  var KEYS = ['bg', 'card', 'fg', 'sub', 'fill', 'accent', 'viz'];
  var VARS = { bg: '--bg', card: '--card', fg: '--fg', sub: '--sub', fill: '--fill', accent: '--accent', viz: '--viz' };
  var LIGHT = { dark: false, bg: '#F2F2F7', card: '#FFFFFF', fg: '#000000', sub: '#8E8E93', fill: '#E9E9EA', accent: '#007AFF', viz: '#E6E6EC' };
  var DARK = { dark: true, bg: '#0E0E11', card: '#1C1C1F', fg: '#FFFFFF', sub: '#98989F', fill: '#3A3A3E', accent: '#0A84FF', viz: '#16171C' };

  var pal = {}, from = {}, to = null, themeStart = 0;

  function rgb(h) { return [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)]; }
  function css(c) { return 'rgb(' + Math.round(c[0]) + ',' + Math.round(c[1]) + ',' + Math.round(c[2]) + ')'; }
  function lighten(c, t) { return [c[0] + (255 - c[0]) * t, c[1] + (255 - c[1]) * t, c[2] + (255 - c[2]) * t]; }

  function applyPalette() {
    var root = document.documentElement.style;
    for (var i = 0; i < KEYS.length; i++) root.setProperty(VARS[KEYS[i]], css(pal[KEYS[i]]));
    var meta = document.querySelector ? document.querySelector('meta[name=theme-color]') : null;
    if (meta) meta.setAttribute('content', css(pal.card));
  }

  function setTheme(t, instant) {
    var next = {};
    for (var i = 0; i < KEYS.length; i++) next[KEYS[i]] = rgb(t[KEYS[i]]);
    var first = !pal.bg;
    for (var j = 0; j < KEYS.length; j++) from[KEYS[j]] = first ? next[KEYS[j]] : pal[KEYS[j]].slice();
    to = next;
    themeStart = (instant || first) ? -1e9 : performance.now();
    document.documentElement.style.colorScheme = t.dark ? 'dark' : 'light';
  }

  function stepTheme(now) {
    if (!to) return;
    var p = Math.min(1, (now - themeStart) / 320);
    var k = 1 - Math.pow(1 - p, 3);
    for (var i = 0; i < KEYS.length; i++) {
      var key = KEYS[i], a = from[key], b = to[key];
      pal[key] = [a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k, a[2] + (b[2] - a[2]) * k];
    }
    applyPalette();
    if (p >= 1) to = null;
  }

  var injected = __THEME__;
  var prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
  setTheme(injected || (prefersDark ? DARK : LIGHT), true);
  stepTheme(performance.now());

  var statusEl = $('status'), btn = $('btn');
  var ws, ctx = null, analyser = null, lock = null, rate = 0, nextTime = 0, lastChunk = -1e9;
  var connected = false, everConnected = false, live = false, listening = false;

  function render() {
    var key = !connected ? (everConnected ? 'lost' : 'connecting')
            : !live ? 'waiting'
            : listening ? 'playing' : 'ready';
    statusEl.textContent = text[key];
    btn.textContent = listening ? text.stop : text.start;
    btn.className = listening ? 'stop' : '';
    btn.disabled = !connected;
  }

  function keepAwake() {
    if (!navigator.wakeLock || !listening) return;
    navigator.wakeLock.request('screen').then(function (l) { lock = l; }).catch(function () {});
  }

  var B = 40, N = 2048;
  var freq = null, edges = null;
  var raw = new Float32Array(B), cur = new Float32Array(B), peak = new Float32Array(B);

  function startListening() {
    var Ctx = window.AudioContext || window.webkitAudioContext;
    if (!Ctx) return;
    try { ctx = rate ? new Ctx({ sampleRate: rate }) : new Ctx(); } catch (e) { ctx = new Ctx(); }
    ctx.resume();

    analyser = ctx.createAnalyser();
    analyser.fftSize = N;
    analyser.smoothingTimeConstant = 0;
    analyser.connect(ctx.destination);
    freq = new Float32Array(analyser.frequencyBinCount);

    var sr = ctx.sampleRate, fmin = 45, fmax = Math.min(14000, sr / 2 * 0.9);
    edges = [];
    for (var b = 0; b <= B; b++) edges.push(Math.round(fmin * Math.pow(fmax / fmin, b / B) * N / sr));
    for (var i = 1; i <= B; i++) if (edges[i] <= edges[i - 1]) edges[i] = edges[i - 1] + 1;

    nextTime = 0;
    listening = true;
    keepAwake();
  }

  function stopListening() {
    listening = false;
    analyser = null;
    if (ctx) { ctx.close(); ctx = null; }
    if (lock) { lock.release(); lock = null; }
  }

  function play(buf) {
    var pcm = new Int16Array(buf);
    var frames = pcm.length >> 1;
    if (!frames || !rate) return;
    try {
      var chunk = ctx.createBuffer(2, frames, rate);
      var l = chunk.getChannelData(0), r = chunk.getChannelData(1);
      for (var i = 0, j = 0; i < frames; i++, j += 2) {
        l[i] = pcm[j] / 32768;
        r[i] = pcm[j + 1] / 32768;
      }
      var now = ctx.currentTime;
      if (nextTime < now + 0.03) nextTime = now + 0.15;
      if (nextTime > now + 0.8) return;
      var src = ctx.createBufferSource();
      src.buffer = chunk;
      src.connect(analyser);
      src.start(nextTime);
      nextTime += chunk.duration;
      lastChunk = performance.now();
    } catch (e) {}
  }

  function readBands() {
    analyser.getFloatFrequencyData(freq);
    for (var b = 0; b < B; b++) {
      var top = -Infinity;
      for (var k = edges[b]; k < edges[b + 1] && k < freq.length; k++) if (freq[k] > top) top = freq[k];
      var v = isFinite(top) ? (top + 6 + 80) / 62 : 0;
      raw[b] = v <= 0 ? 0 : Math.min(1, v + 0.15 * b / B);
    }
  }

  var cv = $('viz'), g = cv.getContext('2d'), dpr = 1, cw = 0, ch = 0;

  function fit() {
    dpr = window.devicePixelRatio || 1;
    cw = cv.clientWidth;
    ch = cv.clientHeight;
    cv.width = Math.round(cw * dpr);
    cv.height = Math.round(ch * dpr);
  }
  window.addEventListener('resize', fit);
  fit();

  function pill(x, y, w, h, r) {
    r = Math.min(r, w / 2, h / 2);
    g.beginPath();
    g.moveTo(x + r, y);
    g.arcTo(x + w, y, x + w, y + h, r);
    g.arcTo(x + w, y + h, x, y + h, r);
    g.arcTo(x, y + h, x, y, r);
    g.arcTo(x, y, x + w, y, r);
    g.closePath();
  }

  var t0 = performance.now(), prev = t0;

  function frame(now) {
    stepTheme(now);
    if (!cw || !ch) fit();

    var t = (now - t0) / 1000;
    var step = Math.max(0.5, Math.min(3, (now - prev) / 16.667));
    prev = now;

    var flowing = listening && analyser && now - lastChunk < 250;
    if (flowing) readBands();

    var intro = Math.min(1, t / 1.1);
    intro = 1 - Math.pow(1 - intro, 3);
    var up = 1 - Math.pow(1 - 0.45, step), down = 1 - Math.pow(1 - 0.12, step), fall = 0.007 * step;

    for (var i = 0; i < B; i++) {
      var idle = 0;
      var target = (flowing ? Math.max(raw[i], 0.04) : idle) * intro;
      cur[i] += (target - cur[i]) * (target > cur[i] ? up : down);
      peak[i] = Math.max(cur[i], peak[i] - fall);
    }

    var k = cw / 380;
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
    g.clearRect(0, 0, cw, ch);
    g.fillStyle = css(pal.viz);
    pill(0, 0, cw, ch, 20 * k);
    g.fill();

    var padX = 18 * k, padY = 16 * k, gap = 3 * k;
    var usableW = cw - 2 * padX, usableH = ch - 2 * padY;
    var bw = (usableW - gap * (B - 1)) / B;
    if (bw >= 1 && usableH >= 4) {
      var baseY = padY + usableH;
      var grad = g.createLinearGradient(0, padY, 0, baseY);
      grad.addColorStop(0, css(lighten(pal.accent, 0.45)));
      grad.addColorStop(1, css(pal.accent));

      for (var n = 0; n < B; n++) {
        var bh = Math.max(bw, Math.min(1, Math.max(0, cur[n])) * usableH);
        var x = padX + n * (bw + gap);
        g.fillStyle = grad;
        pill(x, baseY - bh, bw, bh, bw / 2);
        g.fill();

        var capTop = baseY - Math.max(bw, Math.min(1, peak[n]) * usableH) - bw - 3 * k;
        if (capTop < baseY - bh - 4 * k && capTop > 0) {
          g.fillStyle = 'rgba(' + Math.round(pal.fg[0]) + ',' + Math.round(pal.fg[1]) + ',' + Math.round(pal.fg[2]) + ',.6)';
          pill(x, capTop, bw, bw * 0.8, bw / 2);
          g.fill();
        }
      }
    }
    requestAnimationFrame(frame);
  }

  function connect() {
    ws = new WebSocket('ws://' + location.host + '/stream');
    ws.binaryType = 'arraybuffer';
    ws.onopen = function () { connected = true; everConnected = true; render(); };
    ws.onmessage = function (e) {
      if (typeof e.data === 'string') {
        var m = JSON.parse(e.data);
        if (m.theme) setTheme(m.theme, false);
        if (m.rate) { rate = m.rate; live = true; }
        else if (m.idle) { live = false; }
        render();
        return;
      }
      if (listening && ctx) play(e.data);
    };
    ws.onclose = function () {
      connected = false;
      live = false;
      render();
      setTimeout(connect, 1500);
    };
  }

  btn.onclick = function () {
    if (listening) stopListening(); else startListening();
    render();
  };

  document.addEventListener('visibilitychange', function () {
    if (document.visibilityState === 'visible' && listening) keepAwake();
  });

  render();
  connect();
  requestAnimationFrame(frame);
})();
</script>
</body>
</html>
""";
}
