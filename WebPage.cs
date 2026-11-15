using System.IO;
using System.Text;

namespace HeadphoneHub;

internal static class WebPage
{
    public static byte[] Render(string? themeJson) =>
        Encoding.UTF8.GetBytes(Source.Replace("__THEME__", themeJson ?? "null"));

    public static byte[] Asset(string name)
    {
        using var stream = typeof(WebPage).Assembly.GetManifestResourceStream(name);
        if (stream == null) return Array.Empty<byte>();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

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
body{background:var(--bg);color:var(--fg);font-family:system-ui,-apple-system,"Segoe UI",Roboto,Tahoma,sans-serif;display:flex;overflow-x:hidden;overflow-y:auto;padding:max(16px,env(safe-area-inset-top)) max(16px,env(safe-area-inset-right)) max(16px,env(safe-area-inset-bottom)) max(16px,env(safe-area-inset-left))}
.wrap{container-type:inline-size;width:min(440px,92vw,60vh);margin:auto;flex:none}
@media (pointer:coarse),(max-aspect-ratio:3/5){.wrap{width:min(78vw,60vh)}}
.card{width:100%;background:var(--card);border-radius:6.4cqw;padding:7.5cqw 6.4cqw;text-align:center;border:max(1px,.3cqw) solid var(--fill);border:max(1px,.3cqw) solid color-mix(in srgb,var(--fg) 14%,transparent);box-shadow:0 10px 40px rgba(0,0,0,.14);animation:rise .6s cubic-bezier(.22,1,.36,1) both}
@keyframes rise{from{opacity:0;transform:translateY(14px)}to{opacity:1;transform:none}}
@keyframes rise2{from{opacity:0;transform:translateY(8px)}to{opacity:1;transform:none}}
.card>:not(canvas){animation:rise2 .6s cubic-bezier(.22,1,.36,1) both}
.card>:nth-child(1){animation-delay:.08s}.card>:nth-child(2){animation-delay:.13s}.card>:nth-child(3){animation-delay:.18s}.card>:nth-child(5){animation-delay:.26s}.card>:nth-child(6){animation-delay:.32s}
.logo{width:17.5cqw;height:17.5cqw;margin:0 auto 3.6cqw;border-radius:28%;background:var(--accent);display:flex;align-items:center;justify-content:center}
.logo svg{width:52%;height:52%;stroke:#fff;fill:none;stroke-width:1.9;stroke-linecap:round;stroke-linejoin:round}
h1{font-size:6.2cqw;margin:0 0 1.4cqw}
#status{color:var(--sub);font-size:3.3cqw;min-height:4.8cqw;margin:0 0 4.6cqw}
canvas{display:block;width:100%;height:30cqw;margin:0 0 4.6cqw}
button{width:100%;border:0;border-radius:4.1cqw;padding:3.8cqw;font:inherit;font-size:4cqw;font-weight:600;color:#fff;background:var(--accent);cursor:pointer;transition:transform .45s cubic-bezier(.22,1,.36,1),opacity .3s,background .3s,color .3s,filter .2s}
button:active{transform:scale(.97);transition-duration:.08s,.3s,.3s,.3s,.2s}
@media (hover:hover){#btn:not(:disabled):hover,.acts button:not(:disabled):hover{filter:brightness(1.07)}}
button:disabled{opacity:.45}
button.stop{background:var(--fill);color:var(--fg)}

.rec{margin-top:4cqw}
.fmts{display:grid;grid-template-columns:1fr 1fr;gap:2.4cqw;margin:0 0 3cqw;text-align:start;transition:opacity .25s}
.fmts.locked{pointer-events:none;opacity:.5}
.grp small{display:block;color:var(--sub);font-size:2.7cqw;font-weight:600;letter-spacing:.04em;text-transform:uppercase;margin:0 0 1cqw 1.2cqw}
.seg{position:relative;display:flex;gap:.7cqw;padding:.7cqw;background:var(--fill);border-radius:3.2cqw}
.seg .thumb{position:absolute;left:0;top:0;width:0;height:0;opacity:0;pointer-events:none;background:var(--card);box-shadow:0 .3cqw 1.2cqw rgba(0,0,0,.18);transition:transform .42s cubic-bezier(.22,1,.36,1),width .42s cubic-bezier(.22,1,.36,1),height .42s cubic-bezier(.22,1,.36,1),opacity .2s}
.seg.t button{position:relative;z-index:1}
.seg.t button.on{background:transparent;box-shadow:none}
.seg button{flex:1;width:auto;padding:1.9cqw 0;border-radius:2.6cqw;font-size:3.2cqw;background:transparent;color:var(--sub)}
.seg button.on{background:var(--card);color:var(--fg);box-shadow:0 .3cqw 1.2cqw rgba(0,0,0,.18)}
.seg button:disabled{opacity:.3;cursor:not-allowed}
#rbtn{display:flex;align-items:center;justify-content:center;gap:2cqw;background:var(--fill);color:var(--fg)}
#rbtn .dot{width:3cqw;height:3cqw;border-radius:50%;background:#ff3b30;flex:none;transition:border-radius .25s,background .25s}
#rbtn.on{background:#ff3b30;color:#fff}
#rbtn.on .dot{background:#fff;border-radius:.8cqw;animation:pulse 1.8s ease-in-out infinite}
@keyframes pulse{0%,100%{opacity:1;transform:scale(1)}50%{opacity:.4;transform:scale(.78)}}

.scrim{position:fixed;inset:0;z-index:50;display:flex;align-items:center;justify-content:center;padding:max(16px,env(safe-area-inset-top)) max(16px,env(safe-area-inset-right)) max(16px,env(safe-area-inset-bottom)) max(16px,env(safe-area-inset-left));background:rgba(0,0,0,0);-webkit-backdrop-filter:blur(0);backdrop-filter:blur(0);opacity:0;visibility:hidden;transition:opacity .24s ease,background .24s ease,-webkit-backdrop-filter .28s ease,backdrop-filter .28s ease,visibility 0s linear .28s}
.scrim.open{opacity:1;visibility:visible;background:rgba(0,0,0,.4);-webkit-backdrop-filter:blur(14px);backdrop-filter:blur(14px);transition:opacity .32s ease,background .32s ease,-webkit-backdrop-filter .5s cubic-bezier(.22,1,.36,1),backdrop-filter .5s cubic-bezier(.22,1,.36,1),visibility 0s}
.dlg{width:min(400px,100%);max-height:100%;overflow-y:auto;background:var(--card);color:var(--fg);border:1px solid var(--fill);border-radius:24px;padding:22px 20px 18px;box-shadow:0 24px 70px rgba(0,0,0,.35);text-align:start;transform:translateY(14px) scale(.975);opacity:0;transition:transform .24s cubic-bezier(.4,0,1,1),opacity .2s ease}
.scrim.open .dlg{transform:none;opacity:1;transition:transform .5s cubic-bezier(.22,1,.36,1) .03s,opacity .32s ease .03s}
.scrim.open .dlg>*{animation:rise2 .5s cubic-bezier(.22,1,.36,1) both}
.scrim.open .dlg>:nth-child(1){animation-delay:.08s}.scrim.open .dlg>:nth-child(2){animation-delay:.11s}.scrim.open .dlg>:nth-child(3){animation-delay:.14s}.scrim.open .dlg>:nth-child(4){animation-delay:.17s}.scrim.open .dlg>:nth-child(5){animation-delay:.2s}.scrim.open .dlg>:nth-child(6){animation-delay:.23s}.scrim.open .dlg>:nth-child(7){animation-delay:.26s}.scrim.open .dlg>:nth-child(8){animation-delay:.29s}
.dlg h2{font-size:19px;margin:0 0 4px}
.dlg .sub{margin:0 0 16px;color:var(--sub);font-size:13px;line-height:1.35}
.dg{margin:0 0 14px}
.dg[hidden]{display:none}
.dg small{display:block;color:var(--sub);font-size:11px;font-weight:600;letter-spacing:.04em;text-transform:uppercase;margin:0 0 6px 4px}
.dlg .seg{display:grid;grid-template-columns:repeat(var(--n,2),1fr);gap:4px;padding:4px;border-radius:14px}
.dlg .seg button{width:auto;padding:9px 4px 7px;border-radius:10px;font-size:14px}
.dlg .seg button[data-reco]::after{content:attr(data-reco);display:block;margin-top:1px;font-size:9.5px;font-weight:600;letter-spacing:.02em;color:var(--accent)}
.dlg .seg button:not([data-reco])::after{content:"\00a0";display:block;font-size:9.5px}
.num{display:flex;align-items:center;gap:8px;padding:4px 4px 4px 12px;background:var(--fill);border-radius:14px}
.num input{flex:1;min-width:0;border:0;outline:0;background:transparent;color:var(--fg);font:inherit;font-size:16px;font-weight:600;padding:8px 0;direction:ltr;text-align:start}
.num span{color:var(--sub);font-size:13px;padding:0 10px}
.num{transition:box-shadow .2s}
.num.bad{box-shadow:0 0 0 2px #ff3b30 inset;animation:nudge .36s cubic-bezier(.36,.07,.19,.97)}
@keyframes nudge{20%{transform:translateX(-5px)}45%{transform:translateX(4px)}70%{transform:translateX(-2px)}100%{transform:none}}
@media (prefers-reduced-motion:reduce){.card,.card>*,.scrim.open .dlg>*,.num.bad,#rbtn.on .dot{animation:none}*{transition-duration:.01ms!important}}
.hint{display:block;margin:6px 4px 0;color:var(--sub);font-size:11.5px;cursor:pointer}
.acts{display:flex;gap:8px;margin-top:6px}
.acts button{flex:1;width:auto;padding:13px 8px;font-size:15px;border-radius:14px}
.acts button.ghost{background:var(--fill);color:var(--fg)}
.acts button.go{flex:1.4}
</style>
</head>
<body>
<div class="wrap"><div class="card">
<div class="logo"><svg viewBox="0 0 24 24"><path d="M3,18 V12 A9,9 0 0 1 21,12 V18 M21,19 A2,2 0 0 1 19,21 H18 A2,2 0 0 1 16,19 V16 A2,2 0 0 1 18,14 H21 Z M3,19 A2,2 0 0 0 5,21 H6 A2,2 0 0 0 8,19 V16 A2,2 0 0 0 6,14 H3 Z"/></svg></div>
<h1 id="title">Headphone Hub</h1>
<p id="status"></p>
<canvas id="viz"></canvas>
<button id="btn" disabled></button>
<div class="rec">
<div class="fmts" id="fmts">
<div class="grp"><small id="lAudio">Audio</small><div class="seg"><button type="button" data-f="ogg">OGG</button><button type="button" data-f="wav">WAV</button></div></div>
<div class="grp"><small id="lVideo">Video</small><div class="seg"><button type="button" data-f="mp4">MP4</button><button type="button" data-f="mkv">MKV</button></div></div>
</div>
<button id="rbtn" type="button" disabled><i class="dot"></i><span id="rlabel"></span></button>
</div>
</div></div>
<div class="scrim" id="scrim" aria-hidden="true"><div class="dlg" role="dialog" aria-modal="true" aria-labelledby="dTitle">
<h2 id="dTitle"></h2>
<p class="sub" id="dSub"></p>
<div class="dg"><small id="dFps"></small><div class="seg" data-k="fps" style="--n:2"><button type="button" data-v="30">30</button><button type="button" data-v="60">60</button></div></div>
<div class="dg"><small id="dAbr"></small><div class="seg" data-k="abr" style="--n:3"><button type="button" data-v="64">64</button><button type="button" data-v="92">92</button><button type="button" data-v="128">128</button><button type="button" data-v="192">192</button><button type="button" data-v="256">256</button><button type="button" data-v="320">320</button></div></div>
<div class="dg" id="gSr"><small id="dSr"></small><div class="seg" data-k="sr" style="--n:2"><button type="button" data-v="44100">44100</button><button type="button" data-v="48000">48000</button></div></div>
<div class="dg"><small id="dVbr"></small><div class="num" id="vbrBox"><input id="vbr" type="number" inputmode="numeric" min="500" max="50000" step="100"><span>kbps</span></div><small class="hint" id="hVbr" style="text-transform:none;letter-spacing:0;font-weight:500"></small></div>
<div class="acts"><button type="button" class="ghost" id="dCancel"></button><button type="button" class="go" id="dGo"></button></div>
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
    stop: 'توقف',
    recAudio: 'صدا',
    recVideo: 'ویدیو',
    rec: 'ضبط',
    recStop: 'پایان ضبط',
    recSaving: 'در حال ذخیره…',
    save: 'ذخیره',
    noFmt: 'این مرورگر از این قالب پشتیبانی نمی‌کند',
    recFail: 'شروع ضبط ممکن نشد',
    recEmpty: 'چیزی ضبط نشد',
    recHint: 'برای ضبط، ابتدا گوش دهید',
    dTitle: 'تنظیمات ضبط ویدیو',
    dSub: 'پیش از شروع ضبط، کیفیت را انتخاب کنید',
    dFps: 'نرخ فریم',
    dAbr: 'بیت‌ریت صدا',
    dSr: 'نرخ نمونه‌برداری',
    dVbr: 'بیت‌ریت ویدیو',
    reco: 'پیشنهادی',
    recoVbr: 'پیشنهادی: ۶۰۰۰ کیلوبیت بر ثانیه (برای برگشت لمس کنید)',
    dGo: 'شروع ضبط',
    dCancel: 'انصراف'
  } : {
    connecting: 'Connecting…',
    lost: 'Connection lost. Reconnecting…',
    waiting: 'Connected. Waiting for audio from the PC…',
    ready: 'Ready to listen',
    playing: 'Listening live',
    start: 'Tap to listen',
    stop: 'Stop',
    recAudio: 'Audio',
    recVideo: 'Video',
    rec: 'Record',
    recStop: 'Stop recording',
    recSaving: 'Saving…',
    save: 'Save',
    noFmt: 'Not supported by this browser',
    recFail: 'Recording could not start',
    recEmpty: 'Nothing was recorded',
    recHint: 'Listen first to record',
    dTitle: 'Video recording',
    dSub: 'Choose the quality before recording starts',
    dFps: 'Frame rate',
    dAbr: 'Audio bitrate',
    dSr: 'Sample rate',
    dVbr: 'Video bitrate',
    reco: 'Recommended',
    recoVbr: 'Recommended: 6000 kbps (tap to reset)',
    dGo: 'Start recording',
    dCancel: 'Cancel'
  };

  if (fa) {
    document.documentElement.lang = 'fa';
    document.documentElement.dir = 'rtl';
    $('title').textContent = 'هدفون هاب';
  }
  $('lAudio').textContent = text.recAudio;
  $('lVideo').textContent = text.recVideo;
  if (navigator.audioSession) navigator.audioSession.type = 'playback';

  var KEYS = ['bg', 'card', 'fg', 'sub', 'fill', 'accent', 'viz'];
  var VARS = { bg: '--bg', card: '--card', fg: '--fg', sub: '--sub', fill: '--fill', accent: '--accent', viz: '--viz' };
  var LIGHT = { dark: false, bg: '#F2F2F7', card: '#FFFFFF', fg: '#000000', sub: '#8E8E93', fill: '#E9E9EA', accent: '#007AFF', viz: '#E6E6EC' };
  var DARK = { dark: true, bg: '#0E0E11', card: '#1C1C1F', fg: '#FFFFFF', sub: '#98989F', fill: '#3A3A3E', accent: '#0A84FF', viz: '#16171C' };

  var pal = {}, from = {}, to = null, themeStart = 0;

  function rgb(h) { return [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)]; }
  function css(c) { return 'rgb(' + Math.round(c[0]) + ',' + Math.round(c[1]) + ',' + Math.round(c[2]) + ')'; }
  function cssA(c, a) { return 'rgba(' + Math.round(c[0]) + ',' + Math.round(c[1]) + ',' + Math.round(c[2]) + ',' + a + ')'; }
  function lighten(c, t) { return [c[0] + (255 - c[0]) * t, c[1] + (255 - c[1]) * t, c[2] + (255 - c[2]) * t]; }

  var palVer = 0;
  function applyPalette() {
    palVer++;
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
  var ws, ctx = null, analyser = null, mon = null, lock = null, rate = 0, nextTime = 0, lastChunk = -1e9;
  var connected = false, everConnected = false, live = false, listening = false;
  var rec = null, tick = 0;

  var swapped = false;
  function swap(el, txt, lift) {
    if (el.textContent === txt) return;
    el.textContent = txt;
    if (!swapped || !el.animate || (window.matchMedia && matchMedia('(prefers-reduced-motion:reduce)').matches)) return;
    el.animate(lift ? [{ opacity: 0, transform: 'translateY(4px)' }, { opacity: 1, transform: 'none' }] : [{ opacity: .4 }, { opacity: 1 }],
               { duration: lift ? 340 : 260, easing: 'cubic-bezier(.22,1,.36,1)' });
  }

  function render() {
    var key = !connected ? (everConnected ? 'lost' : 'connecting')
            : !live ? 'waiting'
            : listening ? 'playing' : 'ready';
    swap(statusEl, text[key], true);
    swap(btn, listening ? text.stop : text.start, false);
    btn.className = listening ? 'stop' : '';
    btn.disabled = !connected;
    renderRec();
  }

  function keepAwake() {
    if (!navigator.wakeLock || !(listening || rec)) return;
    if (lock && !lock.released) return;
    navigator.wakeLock.request('screen').then(function (l) { lock = l; }).catch(function () {});
  }

  var B = 40, N = 2048;
  var freq = null, edges = null;
  var raw = new Float32Array(B), cur = new Float32Array(B), peak = new Float32Array(B);

  function ensureEngine() {
    if (ctx) { ctx.resume(); return true; }
    var Ctx = window.AudioContext || window.webkitAudioContext;
    if (!Ctx) return false;
    try { ctx = rate ? new Ctx({ sampleRate: rate }) : new Ctx(); } catch (e) { ctx = new Ctx(); }
    ctx.resume();

    analyser = ctx.createAnalyser();
    analyser.fftSize = N;
    analyser.smoothingTimeConstant = 0;
    mon = ctx.createGain();
    mon.gain.value = listening ? 1 : 0;
    analyser.connect(mon);
    mon.connect(ctx.destination);
    freq = new Float32Array(analyser.frequencyBinCount);

    var sr = ctx.sampleRate, fmin = 45, fmax = Math.min(14000, sr / 2 * 0.9);
    edges = [];
    for (var b = 0; b <= B; b++) edges.push(Math.round(fmin * Math.pow(fmax / fmin, b / B) * N / sr));
    for (var i = 1; i <= B; i++) if (edges[i] <= edges[i - 1]) edges[i] = edges[i - 1] + 1;

    nextTime = 0;
    return true;
  }

  function releaseIfIdle() {
    if (listening || rec) return;
    analyser = null;
    mon = null;
    if (ctx) { try { ctx.close(); } catch (e) {} ctx = null; }
    if (lock) { try { lock.release(); } catch (e) {} lock = null; }
  }

  function startListening() {
    if (!ensureEngine()) return;
    listening = true;
    mon.gain.value = 1;
    keepAwake();
  }

  function stopListening() {
    if (rec) stopRecording();
    listening = false;
    if (mon) mon.gain.value = 0;
    releaseIfIdle();
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
      var st = nextTime;
      src.start(st);
      nextTime += chunk.duration;
      lastChunk = performance.now();
      if (rec && rec.wav && !rec.ending) recWav(pcm, st);
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

  function pill(c, x, y, w, h, r) {
    r = Math.min(r, w / 2, h / 2);
    c.beginPath();
    c.moveTo(x + r, y);
    c.arcTo(x + w, y, x + w, y + h, r);
    c.arcTo(x + w, y + h, x, y + h, r);
    c.arcTo(x, y + h, x, y, r);
    c.arcTo(x, y, x + w, y, r);
    c.closePath();
  }

  function drawViz(c, w, h, k) {
    c.fillStyle = css(pal.viz);
    pill(c, 0, 0, w, h, 20 * k);
    c.fill();

    var padX = 18 * k, padY = 16 * k, gap = 3 * k;
    var usableW = w - 2 * padX, usableH = h - 2 * padY;
    var bw = (usableW - gap * (B - 1)) / B;
    if (bw < 1 || usableH < 4) return;
    var baseY = padY + usableH;
    var grad = c.createLinearGradient(0, padY, 0, baseY);
    grad.addColorStop(0, css(lighten(pal.accent, 0.45)));
    grad.addColorStop(1, css(pal.accent));

    for (var n = 0; n < B; n++) {
      var bh = Math.max(bw, Math.min(1, Math.max(0, cur[n])) * usableH);
      var x = padX + n * (bw + gap);
      c.fillStyle = grad;
      pill(c, x, baseY - bh, bw, bh, bw / 2);
      c.fill();

      var capTop = baseY - Math.max(bw, Math.min(1, peak[n]) * usableH) - bw - 3 * k;
      if (capTop < baseY - bh - 4 * k && capTop > 0) {
        c.fillStyle = cssA(pal.fg, 0.6);
        pill(c, x, capTop, bw, bw * 0.8, bw / 2);
        c.fill();
      }
    }
  }

  var MR = typeof MediaRecorder !== 'undefined' ? MediaRecorder : null;
  var PLAN = {};

  function allTypes(list) {
    var out = [];
    if (!MR || !MR.isTypeSupported) return out;
    for (var i = 0; i < list.length; i++) {
      try { if (MR.isTypeSupported(list[i])) out.push(list[i]); } catch (e) {}
    }
    return out;
  }

  (function detect() {
    var m;
    PLAN.wav = { ext: 'wav', mime: 'audio/wav' };
    var canNode = !!(window.AudioContext || window.webkitAudioContext);
    if (!canNode) return;

    m = allTypes(['audio/ogg;codecs=opus', 'audio/ogg']);
    if (m.length) PLAN.ogg = { ext: 'ogg', mime: 'audio/ogg', recs: m, fix: null };
    else {
      m = allTypes(['audio/webm;codecs=opus', 'audio/webm']);
      if (m.length) PLAN.ogg = { ext: 'ogg', mime: 'audio/ogg', recs: m, fix: 'ogg' };
    }

    var canCanvas = !!(window.HTMLCanvasElement && HTMLCanvasElement.prototype.captureStream);
    if (!canCanvas) return;

    var h264 = allTypes(['video/x-matroska;codecs=avc1', 'video/x-matroska;codecs=h264', 'video/webm;codecs=h264', 'video/webm;codecs=avc1']);
    var other = allTypes(['video/webm;codecs=vp9', 'video/webm;codecs=vp8', 'video/webm;codecs=av1', 'video/webm']);

    if (h264.length) PLAN.mp4 = { ext: 'mp4', mime: 'video/mp4', recs: h264, video: true, mux: 'mp4' };
    else {
      var apple = /Apple/.test(navigator.vendor || '') && !/Chrome|Chromium|Edg|Android/.test(navigator.userAgent || '');
      m = allTypes(apple ? ['video/mp4;codecs=avc1.640028,mp4a.40.2', 'video/mp4;codecs=avc1,mp4a.40.2', 'video/mp4']
                         : ['video/mp4;codecs=avc1.640028,mp4a.40.2', 'video/mp4;codecs=avc1.42E01E,mp4a.40.2', 'video/mp4;codecs=avc1,mp4a.40.2']);
      if (m.length) PLAN.mp4 = { ext: 'mp4', mime: 'video/mp4', recs: m, video: true, mux: false };
    }

    m = h264.concat(other);
    if (m.length) PLAN.mkv = { ext: 'mkv', mime: 'video/x-matroska', recs: m, video: true, mux: 'mkv' };
  })();

  var fmt = 'wav';
  try { fmt = localStorage.getItem('hh.fmt') || ''; } catch (e) {}
  if (!PLAN[fmt]) fmt = PLAN.ogg ? 'ogg' : 'wav';

  var fmtsEl = $('fmts'), rbtn = $('rbtn'), rlabel = $('rlabel');
  var fmtBtns = fmtsEl.querySelectorAll('button');

  (function () {
    var all = document.querySelectorAll('.seg');
    for (var i = 0; i < all.length; i++) {
      var th = document.createElement('i');
      th.className = 'thumb';
      all[i].insertBefore(th, all[i].firstChild);
      all[i].className += ' t';
    }
  })();
  function placeSegs(force) {
    var all = document.querySelectorAll('.seg');
    for (var i = 0; i < all.length; i++) {
      var s = all[i], t = s.firstChild, on = s.querySelector('button.on');
      if (!t || t.className !== 'thumb') continue;
      if (!on || !on.offsetWidth) { t.style.opacity = 0; t._ok = false; continue; }
      if (!force && t._on === on && t._ok) continue;
      var still = !t._ok || force;
      if (still) t.style.transition = 'none';
      t.style.borderRadius = getComputedStyle(on).borderRadius;
      t.style.width = on.offsetWidth + 'px';
      t.style.height = on.offsetHeight + 'px';
      t.style.transform = 'translate(' + on.offsetLeft + 'px,' + on.offsetTop + 'px)';
      t.style.opacity = 1;
      if (still) { void t.offsetWidth; t.style.transition = ''; }
      t._on = on; t._ok = true;
    }
  }
  window.addEventListener('resize', function () { placeSegs(true); });
  if (document.fonts && document.fonts.ready) document.fonts.ready.then(function () { placeSegs(true); });

  fmtsEl.addEventListener('click', function (e) {
    var b = e.target;
    while (b && b !== fmtsEl && b.tagName !== 'BUTTON') b = b.parentNode;
    if (!b || b === fmtsEl || b.disabled || rec) return;
    fmt = b.getAttribute('data-f');
    try { localStorage.setItem('hh.fmt', fmt); } catch (err) {}
    renderRec();
  });

  function fmtSize(n) {
    if (n < 1048576) return Math.max(1, Math.round(n / 1024)) + ' KB';
    return (n / 1048576).toFixed(n < 10485760 ? 2 : 1) + ' MB';
  }
  function fmtTime(s) {
    s = Math.max(0, Math.floor(s));
    var h = Math.floor(s / 3600), m = Math.floor(s % 3600 / 60), x = s % 60;
    return (h ? h + ':' + (m < 10 ? '0' : '') : '') + m + ':' + (x < 10 ? '0' : '') + x;
  }
  function elapsed() { return rec ? ((rec.ending ? rec.endWall : performance.now()) - rec.startWall) / 1000 : 0; }

  function renderRec() {
    for (var i = 0; i < fmtBtns.length; i++) {
      var f = fmtBtns[i].getAttribute('data-f'), ok = !!PLAN[f];
      fmtBtns[i].disabled = !ok;
      fmtBtns[i].title = ok ? '' : text.noFmt;
      fmtBtns[i].className = f === fmt ? 'on' : '';
    }
    if (typeof dlgOpen !== 'undefined' && dlgOpen && (rec || !(connected && listening))) closeDialog();
    fmtsEl.className = rec ? 'fmts locked' : 'fmts';
    placeSegs();
    var recording = rec && !rec.ending;
    rbtn.className = recording ? 'on' : '';
    rbtn.disabled = rec ? !!rec.ending : !(connected && listening);
    rlabel.textContent = !rec ? (listening ? text.rec + ' ' + fmt.toUpperCase() : text.recHint)
                       : rec.ending ? text.recSaving
                       : text.recStop + ' · ' + fmtTime(elapsed()) + ' · ' + fmtSize(rec.bytes);
  }

  function resample(pcm, a, b) {
    var n = pcm.length >> 1, m = Math.max(1, Math.round(n * b / a)), out = new Int16Array(m * 2);
    for (var i = 0; i < m; i++) {
      var p = i * a / b, i0 = Math.floor(p), i1 = Math.min(n - 1, i0 + 1), f = p - i0;
      i0 = Math.min(n - 1, i0);
      out[2 * i] = pcm[2 * i0] * (1 - f) + pcm[2 * i1] * f;
      out[2 * i + 1] = pcm[2 * i0 + 1] * (1 - f) + pcm[2 * i1 + 1] * f;
    }
    return out;
  }

  function recWav(pcm, st) {
    var w = rec.wav;
    if (!w.rate) w.rate = rate;
    var data = rate === w.rate ? pcm : resample(pcm, rate, w.rate);
    var frames = data.length >> 1;
    var offset = Math.round(Math.max(0, st - rec.startCtx) * w.rate);
    if (offset > w.frames) {
      w.put(new Int16Array((offset - w.frames) * 2));
      w.frames = offset;
    } else if (offset < w.frames) {
      var skip = w.frames - offset;
      if (skip >= frames) return;
      data = data.subarray(skip * 2);
      frames -= skip;
    }
    w.put(data);
    w.frames += frames;
    if (rec.fmt === 'wav') rec.bytes = 44 + w.frames * 4;
  }

  function makeWav(w) {
    var rt = w.rate || rate || 48000, size = w.frames * 4;
    var h = new DataView(new ArrayBuffer(44));
    function s(o, t) { for (var i = 0; i < t.length; i++) h.setUint8(o + i, t.charCodeAt(i)); }
    s(0, 'RIFF'); h.setUint32(4, 36 + size, true); s(8, 'WAVE'); s(12, 'fmt ');
    h.setUint32(16, 16, true); h.setUint16(20, 1, true); h.setUint16(22, 2, true);
    h.setUint32(24, rt, true); h.setUint32(28, rt * 4, true); h.setUint16(32, 4, true); h.setUint16(34, 16, true);
    s(36, 'data'); h.setUint32(40, size, true);
    return new Blob([h.buffer].concat(w.parts), { type: 'audio/wav' });
  }

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

  function parseWebm(u) {
    var p = 0, end = u.length, head = null, packets = [];
    while (p < end - 2) {
      var a = ebmlId(u, p), s = ebmlSize(u, p + a.n);
      if (!s) break;
      var ds = p + a.n + s.n;
      if (a.id === 0x18538067 || a.id === 0x1F43B675 || a.id === 0x1654AE6B || a.id === 0xAE || a.id === 0xA0) { p = ds; continue; }
      if (s.size < 0) break;
      var len = Math.min(s.size, end - ds);
      if (a.id === 0x63A2) head = u.slice(ds, ds + len);
      else if (a.id === 0xA3 || a.id === 0xA1) {
        var t = ebmlSize(u, ds);
        if (t && len > t.n + 3) packets.push(u.subarray(ds + t.n + 3, ds + len));
      }
      p = ds + len;
    }
    return { head: head, packets: packets };
  }

  function opusSamples(p) {
    var toc = p[0], cfg = toc >> 3, c = toc & 3, fs;
    if (cfg < 12) fs = [480, 960, 1920, 2880][cfg & 3];
    else if (cfg < 16) fs = [480, 960][cfg & 1];
    else fs = [120, 240, 480, 960][cfg & 3];
    return fs * (c === 0 ? 1 : c === 3 ? (p[1] & 63) : 2);
  }

  var CRC = (function () {
    var t = new Uint32Array(256);
    for (var i = 0; i < 256; i++) {
      var r = i << 24;
      for (var j = 0; j < 8; j++) r = (r & 0x80000000) ? ((r << 1) ^ 0x04C11DB7) : (r << 1);
      t[i] = r >>> 0;
    }
    return t;
  })();

  function oggPage(pks, type, gran, serial, seq) {
    var segs = [], body = 0, i, n;
    for (i = 0; i < pks.length; i++) {
      n = pks[i].length; body += n;
      while (n >= 255) { segs.push(255); n -= 255; }
      segs.push(n);
    }
    var page = new Uint8Array(27 + segs.length + body), dv = new DataView(page.buffer);
    page.set([0x4F, 0x67, 0x67, 0x53, 0, type]);
    dv.setUint32(6, gran % 4294967296, true);
    dv.setUint32(10, Math.floor(gran / 4294967296), true);
    dv.setUint32(14, serial, true);
    dv.setUint32(18, seq, true);
    page[26] = segs.length;
    page.set(segs, 27);
    var o = 27 + segs.length;
    for (i = 0; i < pks.length; i++) { page.set(pks[i], o); o += pks[i].length; }
    var c = 0;
    for (i = 0; i < page.length; i++) c = ((c << 8) ^ CRC[((c >>> 24) ^ page[i]) & 255]) >>> 0;
    dv.setUint32(22, c, true);
    return page;
  }

  function str(s) { var a = []; for (var i = 0; i < s.length; i++) a.push(s.charCodeAt(i)); return a; }

  function muxOgg(u) {
    var d = parseWebm(u);
    if (!d.packets.length) throw new Error('no audio');
    var head = d.head;
    if (!head || head.length < 19 || String.fromCharCode.apply(null, head.subarray(0, 8)) !== 'OpusHead') {
      head = new Uint8Array([].concat(str('OpusHead'), [1, (d.packets[0][0] & 4) ? 2 : 1, 312 & 255, 312 >> 8, 0x80, 0xBB, 0, 0, 0, 0, 0]));
    }
    var vendor = str('HeadphoneHub');
    var tags = new Uint8Array([].concat(str('OpusTags'), [vendor.length, 0, 0, 0], vendor, [0, 0, 0, 0]));
    var serial = (Math.random() * 4294967295) >>> 0, seq = 0, pages = [];
    pages.push(oggPage([head], 0x02, 0, serial, seq++));
    pages.push(oggPage([tags], 0, 0, serial, seq++));

    var group = [], segCount = 0, total = 0, i;
    function flush(last) {
      pages.push(oggPage(group, last ? 0x04 : 0, total, serial, seq++));
      group = []; segCount = 0;
    }
    for (i = 0; i < d.packets.length; i++) {
      var pk = d.packets[i], need = Math.floor(pk.length / 255) + 1;
      if (group.length && (segCount + need > 255 || group.length >= 50)) flush(false);
      group.push(pk); segCount += need; total += opusSamples(pk);
    }
    flush(true);
    return new Blob(pages, { type: 'audio/ogg' });
  }

  function webmToOgg(blob) {
    return blob.arrayBuffer().then(function (ab) { return muxOgg(new Uint8Array(ab)); });
  }

  var rcv = null, rg = null, statC = null, statVer = -1, iconPath = null;
  try { iconPath = new Path2D('M3,18 V12 A9,9 0 0 1 21,12 V18 M21,19 A2,2 0 0 1 19,21 H18 A2,2 0 0 1 16,19 V16 A2,2 0 0 1 18,14 H21 Z M3,19 A2,2 0 0 0 5,21 H6 A2,2 0 0 0 8,19 V16 A2,2 0 0 0 6,14 H3 Z'); } catch (e) {}
  var FONT = 'system-ui,-apple-system,"Segoe UI",Roboto,Tahoma,sans-serif';
  var RW = 1280, RH = 720, CW2 = 860, CH2 = 600, CX = (RW - CW2) / 2, CY = (RH - CH2) / 2, LS = 96, LY = CY + 44;

  function paintStatic(c) {
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.fillStyle = css(pal.bg);
    c.fillRect(0, 0, RW, RH);

    c.save();
    c.shadowColor = 'rgba(0,0,0,.18)'; c.shadowBlur = 60; c.shadowOffsetY = 16;
    c.fillStyle = css(pal.card);
    pill(c, CX, CY, CW2, CH2, 52);
    c.fill();
    c.restore();
    c.lineWidth = 2;
    c.strokeStyle = cssA(pal.fg, 0.14);
    pill(c, CX, CY, CW2, CH2, 52);
    c.stroke();

    var lx = RW / 2 - LS / 2;
    c.fillStyle = css(pal.accent);
    pill(c, lx, LY, LS, LS, LS * 0.28);
    c.fill();
    if (iconPath) {
      c.save();
      c.translate(lx + LS * 0.24, LY + LS * 0.24);
      c.scale(LS * 0.52 / 24, LS * 0.52 / 24);
      c.strokeStyle = '#fff'; c.lineWidth = 1.9; c.lineCap = 'round'; c.lineJoin = 'round';
      c.stroke(iconPath);
      c.restore();
    }

    c.textAlign = 'center';
    c.textBaseline = 'alphabetic';
    c.fillStyle = css(pal.fg);
    c.font = '700 44px ' + FONT;
    c.fillText($('title').textContent, RW / 2, LY + LS + 58);
  }

  function drawRec() {
    var c = rg;
    if (!statC) { statC = document.createElement('canvas'); statC.width = RW; statC.height = RH; }
    if (statVer !== palVer) { paintStatic(statC.getContext('2d')); statVer = palVer; }
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.drawImage(statC, 0, 0);

    c.textAlign = 'center';
    c.textBaseline = 'alphabetic';
    c.fillStyle = css(pal.sub);
    c.font = '500 24px ' + FONT;
    c.fillText(fmtTime(elapsed()), RW / 2, LY + LS + 98);

    var vx = CX + 56, vy = LY + LS + 124, vw = CW2 - 112, vh = CY + CH2 - 48 - vy;
    c.save();
    c.translate(vx, vy);
    drawViz(c, vw, vh, vw / 380);
    c.restore();
  }

  function startRecording(cfg) {
    var plan = PLAN[fmt];
    if (rec || !plan || !connected || !listening) return;
    cfg = cfg || CFG;
    if (plan.mux && !muxReady()) return fail(text.recFail);
    if (!ensureEngine()) return fail();
    keepAwake();
    var r = { fmt: fmt, cfg: cfg, mux: plan.mux, fps: cfg.fps, next: 0, startCtx: ctx.currentTime, startWall: performance.now(), endWall: 0, bytes: 0, chunks: [], ending: false, done: false, got: false, err: null };
    rec = r;
    try {
      if (fmt === 'wav') {
        var w = r.wav = { rate: 0, frames: 0, parts: [] };
        w.put = function (d) { w.parts.push(d); };
        r.bytes = 44;
      } else {
        var vopts = {};
        if (plan.video) {
          if (!rcv) {
            rcv = document.createElement('canvas');
            rcv.width = RW; rcv.height = RH;
            rg = rcv.getContext('2d', { alpha: false });
          }
          r.vid = true;
          drawRec();
          vopts.videoBitsPerSecond = cfg.vbr * 1000;
        }
        if (plan.mux) {
          r.mp3 = new HHMux.Mp3Encoder(cfg.sr, cfg.abr);
          r.wav = { rate: cfg.sr, frames: 0 };
          r.wav.put = function (d) { r.mp3.put(d); };
          r.vt = rcv.captureStream().getVideoTracks();
          r.stream = new MediaStream(r.vt);
          vopts.videoKeyFrameIntervalDuration = 1000;
        } else {
          var dest = ctx.createMediaStreamDestination();
          analyser.connect(dest);
          r.dest = dest;
          var stream = dest.stream;
          if (plan.video) {
            r.vt = rcv.captureStream(cfg.fps).getVideoTracks();
            stream = new MediaStream(r.vt.concat(dest.stream.getAudioTracks()));
          }
          r.stream = stream;
        }
        r.cfgs = [];
        for (var i = 0; i < plan.recs.length; i++) {
          var o = { mimeType: plan.recs[i] };
          for (var key in vopts) o[key] = vopts[key];
          if (!plan.mux) o.audioBitsPerSecond = plan.video ? cfg.abr * 1000 : 192000;
          r.cfgs.push(o);
        }
        r.cfgs.push({ mimeType: plan.recs[0] });
        r.ci = 0;
        if (!launch(r)) throw (r.err || new Error('no recorder'));
      }
    } catch (e) {
      try { if (r.dest) analyser.disconnect(r.dest); } catch (x) {}
      if (r.vt) r.vt.forEach(function (t) { try { t.stop(); } catch (y) {} });
      rec = null;
      releaseIfIdle();
      try { console.error(e); } catch (z) {}
      return fail(text.recFail + (e && e.name ? ' (' + e.name + ')' : ''));
    }
    tick = setInterval(renderRec, 250);
    renderRec();
  }

  function launch(r) {
    while (r.ci < r.cfgs.length) {
      var cfg = r.cfgs[r.ci++], mr;
      try { mr = new MR(r.stream, cfg); mr.start(1000); } catch (e) { r.err = e; continue; }
      r.mr = mr; r.mime = cfg.mimeType; r.got = false; r.chunks = []; r.bytes = 0;
      (function (m) {
        m.ondataavailable = function (e) {
          if (r.mr !== m) return;
          if (e.data && e.data.size) { r.got = true; r.chunks.push(e.data); r.bytes += e.data.size; }
        };
        m.onerror = function (ev) {
          if (r.mr !== m) return;
          r.err = ev && ev.error ? ev.error : r.err;
          if (!r.got && !r.ending) retry(r); else if (!r.ending) stopRecording();
        };
        m.onstop = function () {
          if (r.mr !== m) return;
          if (!r.got && !r.ending && retry(r)) return;
          finalize(r);
        };
      })(mr);
      clearTimeout(r.wd);
      r.wd = setTimeout(function () { if (rec === r && !r.got && !r.ending && r.mr) retry(r); }, 3500);
      return true;
    }
    return false;
  }

  function retry(r) {
    var old = r.mr;
    r.mr = null;
    try { if (old && old.state !== 'inactive') old.stop(); } catch (e) {}
    if (launch(r)) return true;
    stopRecording();
    return false;
  }

  function fail(msg) {
    statusEl.textContent = msg || text.recFail;
    setTimeout(render, 4000);
  }

  function stopRecording() {
    var r = rec;
    if (!r || r.ending) return;
    r.ending = true;
    r.endWall = performance.now();
    clearTimeout(r.wd);
    renderRec();
    if (r.mr && r.mr.state !== 'inactive') {
      try { r.mr.stop(); } catch (e) { finalize(r); }
    } else finalize(r);
  }

  function stamp() {
    var d = new Date(), z = function (n) { return (n < 10 ? '0' : '') + n; };
    return d.getFullYear() + z(d.getMonth() + 1) + z(d.getDate()) + '-' + z(d.getHours()) + z(d.getMinutes()) + z(d.getSeconds());
  }

  function finalize(r) {
    if (r.done) return;
    r.done = true;
    clearTimeout(r.wd);
    if (r.vt) r.vt.forEach(function (t) { try { t.stop(); } catch (e) {} });
    try { if (r.dest && analyser) analyser.disconnect(r.dest); } catch (e) {}
    var plan = PLAN[r.fmt], p;
    if (r.fmt === 'wav') p = Promise.resolve({ blob: makeWav(r.wav), ext: 'wav' });
    else if (r.mux) p = finishMux(r, plan);
    else {
      var rawBlob = new Blob(r.chunks, { type: r.mime || plan.mime });
      if (!rawBlob.size) p = Promise.resolve({ blob: rawBlob, ext: plan.ext });
      else if (plan.fix === 'ogg') p = webmToOgg(rawBlob).then(function (b) { return { blob: b, ext: 'ogg' }; }, function () { return { blob: rawBlob, ext: 'webm' }; });
      else p = Promise.resolve({ blob: new Blob(r.chunks, { type: plan.mime }), ext: plan.ext });
    }
    p.then(function (o) {
      rec = null;
      if (tick) { clearInterval(tick); tick = 0; }
      releaseIfIdle();
      if (o.blob.size) saveFile(o.blob, 'HeadphoneHub-' + stamp() + '.' + o.ext);
      else fail(r.err && r.err.name ? text.recFail + ' (' + r.err.name + ')' : text.recEmpty);
      renderRec();
    });
  }

  function finishMux(r, plan) {
    var raw = new Blob(r.chunks, { type: r.mime || 'video/webm' });
    return HHMux.parse(raw).then(HHMux.prepareVideo).then(function (v) {
      if (plan.mux === 'mp4' && v.kind !== 'avc') throw new Error('mp4 needs h264');
      var sr = r.cfg.sr, need = Math.round(v.durMs / 1000 * sr) - r.wav.frames;
      while (need > 0) {
        var n = Math.min(need, sr);
        r.mp3.put(new Int16Array(n * 2));
        need -= n;
      }
      var mp3 = HHMux.splitMp3(r.mp3.finish());
      var audio = mp3.frames.length ? { rate: mp3.rate, frames: mp3.frames } : null;
      var blob = plan.mux === 'mp4' ? HHMux.mp4(v, audio, RW, RH) : HHMux.mkv(v, audio, RW, RH);
      return { blob: blob, ext: plan.ext };
    }).then(null, function (err) {
      try { console.error(err); } catch (e) {}
      return { blob: raw, ext: 'webm' };
    });
  }

  function download(url, name) {
    var a = document.createElement('a');
    a.href = url; a.download = name; a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    setTimeout(function () { if (a.parentNode) a.parentNode.removeChild(a); }, 200);
  }

  function saveFile(blob, name) {
    var url = URL.createObjectURL(blob);
    download(url, name);
    setTimeout(function () { URL.revokeObjectURL(url); }, 60000);
  }

  var CFG = { fps: 30, abr: 192, sr: 44100, vbr: 6000 };
  var RECO = { fps: 30, abr: 192, sr: 44100 }, VBR_MIN = 500, VBR_MAX = 50000, VBR_DEF = 6000;
  var OPTS = { fps: [30, 60], abr: [64, 92, 128, 192, 256, 320], sr: [44100, 48000] };
  try {
    var saved = JSON.parse(localStorage.getItem('hh.cfg3') || 'null');
    if (saved) {
      for (var sk in OPTS) if (OPTS[sk].indexOf(saved[sk]) >= 0) CFG[sk] = saved[sk];
      if (saved.vbr >= VBR_MIN && saved.vbr <= VBR_MAX) CFG.vbr = Math.round(saved.vbr);
    }
  } catch (e) {}

  var scrim = $('scrim'), vbrIn = $('vbr'), vbrBox = $('vbrBox'), dlgOpen = false, lastFocus = null;
  var segs = scrim.querySelectorAll('.seg');
  $('dTitle').textContent = text.dTitle;
  $('dSub').textContent = text.dSub;
  $('dFps').textContent = text.dFps;
  $('dAbr').textContent = text.dAbr;
  $('dSr').textContent = text.dSr;
  $('dVbr').textContent = text.dVbr;
  $('hVbr').textContent = text.recoVbr;
  $('dGo').textContent = text.dGo;
  $('dCancel').textContent = text.dCancel;
  (function () {
    var unit = { fps: ' fps', abr: ' kbps', sr: ' Hz' };
    for (var i = 0; i < segs.length; i++) {
      var k = segs[i].getAttribute('data-k'), bs = segs[i].querySelectorAll('button');
      for (var j = 0; j < bs.length; j++) {
        var v = +bs[j].getAttribute('data-v');
        bs[j].textContent = v + unit[k];
        if (v === RECO[k]) bs[j].setAttribute('data-reco', text.reco);
      }
    }
  })();

  function paintDialog() {
    for (var i = 0; i < segs.length; i++) {
      var k = segs[i].getAttribute('data-k'), bs = segs[i].querySelectorAll('button');
      for (var j = 0; j < bs.length; j++) bs[j].className = +bs[j].getAttribute('data-v') === CFG[k] ? 'on' : '';
    }
    vbrIn.value = CFG.vbr;
    vbrBox.className = 'num';
    $('gSr').hidden = !(PLAN[fmt] && PLAN[fmt].mux);
    placeSegs();
  }

  function readVbr() {
    var v = parseInt(vbrIn.value, 10);
    if (!(v >= VBR_MIN && v <= VBR_MAX)) return null;
    return v;
  }

  function openDialog() {
    if (dlgOpen) return;
    dlgOpen = true;
    lastFocus = document.activeElement;
    if (PLAN[fmt] && PLAN[fmt].mux) loadMux().then(null, function () {});
    paintDialog();
    scrim.setAttribute('aria-hidden', 'false');
    scrim.className = 'scrim open';
    setTimeout(function () { try { $('dGo').focus(); } catch (e) {} }, 60);
  }

  function closeDialog() {
    if (!dlgOpen) return;
    dlgOpen = false;
    scrim.setAttribute('aria-hidden', 'true');
    scrim.className = 'scrim';
    try { if (lastFocus && lastFocus.focus) lastFocus.focus(); } catch (e) {}
  }

  function confirmDialog() {
    var v = readVbr();
    if (v === null) {
      vbrBox.className = 'num';
      void vbrBox.offsetWidth;
      vbrBox.className = 'num bad';
      try { vbrIn.focus(); } catch (e) {}
      return;
    }
    CFG.vbr = v;
    try { localStorage.setItem('hh.cfg3', JSON.stringify(CFG)); } catch (e) {}
    var plan = PLAN[fmt], cfg = { fps: CFG.fps, abr: CFG.abr, sr: CFG.sr, vbr: CFG.vbr };
    closeDialog();
    if (plan && plan.mux && !muxReady()) loadMux().then(function () { startRecording(cfg); }, function () { fail(text.recFail); });
    else startRecording(cfg);
  }

  scrim.addEventListener('click', function (e) {
    var b = e.target;
    if (b === scrim) return closeDialog();
    while (b && b !== scrim && b.tagName !== 'BUTTON') b = b.parentNode;
    if (!b || b === scrim || !b.parentNode || !b.parentNode.getAttribute('data-k')) return;
    CFG[b.parentNode.getAttribute('data-k')] = +b.getAttribute('data-v');
    paintDialog();
  });
  vbrIn.addEventListener('input', function () { vbrBox.className = readVbr() === null && vbrIn.value !== '' ? 'num bad' : 'num'; });
  vbrIn.addEventListener('blur', function () {
    var v = parseInt(vbrIn.value, 10);
    if (isNaN(v)) v = VBR_DEF;
    vbrIn.value = Math.min(VBR_MAX, Math.max(VBR_MIN, v));
    vbrBox.className = 'num';
  });
  $('hVbr').onclick = function () { vbrIn.value = VBR_DEF; vbrBox.className = 'num'; };
  $('dCancel').onclick = closeDialog;
  $('dGo').onclick = confirmDialog;
  document.addEventListener('keydown', function (e) {
    if (!dlgOpen) return;
    if (e.key === 'Escape') closeDialog();
    else if (e.key === 'Enter' && e.target === vbrIn) confirmDialog();
  });

  var muxP = null;
  function muxReady() { return !!(window.HHMux && window.lamejs); }
  function loadScript(src) {
    return new Promise(function (ok, no) {
      var s = document.createElement('script');
      s.src = src; s.onload = ok; s.onerror = no;
      document.head.appendChild(s);
    });
  }
  function loadMux() {
    if (muxReady()) return Promise.resolve();
    if (!muxP) muxP = loadScript('/lame.js').then(function () { return loadScript('/mux.js'); })
      .then(function () { if (!muxReady()) throw new Error('mux'); }, function (e) { muxP = null; throw e; });
    return muxP;
  }

  rbtn.onclick = function () {
    if (rec) stopRecording();
    else if (PLAN[fmt] && PLAN[fmt].video) openDialog();
    else startRecording();
  };

  var t0 = performance.now(), prev = t0;

  function frame(now) {
    stepTheme(now);
    if (!cw || !ch) fit();

    var t = (now - t0) / 1000;
    var step = Math.max(0.5, Math.min(3, (now - prev) / 16.667));
    prev = now;

    var flowing = analyser && now - lastChunk < 250;
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
    drawViz(g, cw, ch, k);

    if (rec && rec.vid && !rec.ending) {
      var iv = 1000 / rec.fps;
      if (!rec.next) rec.next = now;
      if (now + 2 >= rec.next) {
        rec.next += iv;
        if (rec.next < now - iv) rec.next = now + iv;
        drawRec();
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
      if (ctx && analyser && listening) play(e.data);
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
    if (document.visibilityState === 'visible' && (listening || rec)) keepAwake();
  });

  render();
  swapped = true;
  connect();
  requestAnimationFrame(frame);
})();
</script>
</body>
</html>
""";
}