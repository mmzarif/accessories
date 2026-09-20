/*
 * Swappable Magnetic Butterfly Wings — hair clip
 * Target: ESP32-C3 (Seeed XIAO ESP32-C3 or bare module)
 * Arduino-ESP32 core 3.x
 *
 * Drives one 9g micro servo (SG90 / AIMIKE class) through a rocker linkage
 * so the wings flap. Wings attach magnetically and swap out.
 *
 * Servo is driven via raw LEDC rather than the ESP32Servo library:
 *   - ESP32Servo defaults to a 16-bit timer width, which is ILLEGAL on C3
 *     (SOC_LEDC_TIMER_BIT_WIDTH == 14). Its C3 guard was historically keyed
 *     on ARDUINO_ESP32C3_DEV and misses ARDUINO_XIAO_ESP32C3.
 *   - Raw LEDC gives us the bool return from ledcAttach() to check.
 *
 * At 50 Hz on C3, resolution must be >= 10 bits (f_min = clk/(1024*2^res),
 * and core 3.x clocks LEDC from the 40 MHz XTAL by default, not 80 MHz APB).
 * 14 bits is the silicon max and sits safely inside the window: 1.22 us/count.
 *
 * Controls: single tap = next mode, double tap = previous, long press = sleep.
 * Hold the button while powering on to start the Wi-Fi config portal.
 */

#include <Arduino.h>
#include <WiFi.h>
#include <WebServer.h>
#include <Preferences.h>
#include <esp_sleep.h>
#include <math.h>

// ───────────────────────── pin map ─────────────────────────
// XIAO ESP32-C3 labels in comments. Constraints that drove these choices:
//   - GPIO0/GPIO1 are NOT broken out on the XIAO — unusable.
//   - GPIO2 (D0) is a strapping pin: must be HIGH/floating at reset. A button
//     here would break booting whenever it's held (and we hold it on purpose).
//   - GPIO8 strapping, GPIO9 = BOOT button, GPIO18/19 = USB. All avoided.
//   - Battery sense must be ADC1 (GPIO0-4); ADC2 dies when Wi-Fi is on.
//   - Deep-sleep wake needs GPIO0-5 (VDD3P3_RTC domain).
static const uint8_t PIN_SERVO    = 3;  // D1  — servo signal
static const uint8_t PIN_VBAT     = 4;  // D2/A2 — 1:2 divider from BAT+. ADC1.
static const uint8_t PIN_BUTTON   = 5;  // D3  — momentary to GND, wake-capable
static const uint8_t PIN_SERVO_EN = 6;  // D4  — gates servo power (load switch, active LOW)
static const uint8_t PIN_LED      = 10; // D10 — status LED via resistor

static_assert(PIN_BUTTON <= 5, "Button must be on GPIO0-5 for deep sleep wake");
static_assert(PIN_VBAT <= 4, "Battery sense must be on ADC1 (GPIO0-4); ADC2 fails with Wi-Fi on");

// ───────────────────────── servo geometry ─────────────────────────
// Tune these to YOUR linkage after assembly. See docs/CALIBRATION.md.
static const uint16_t SERVO_MIN_US  = 700;   // mechanical floor — never command below
static const uint16_t SERVO_MAX_US  = 2300;  // mechanical ceiling
static const uint16_t WING_DOWN_US  = 1150;  // wings at rest/down
static const uint16_t WING_UP_US    = 1850;  // wings at full upstroke

static const uint32_t LEDC_FREQ_HZ  = 50;
static const uint8_t  LEDC_RES_BITS = 14;    // max on C3; 1.22 us/count at 50 Hz

// ───────────────────────── behaviour ─────────────────────────
static const uint32_t IDLE_SLEEP_MS   = 10UL * 60UL * 1000UL;  // auto-sleep after 10 min
static const uint32_t FRAME_INTERVAL_MS = 20;                  // 50 Hz update = one per servo frame
static const float    VBAT_CUTOFF_V   = 3.40f;                 // stop flapping, protect the LiPo
static const float    VBAT_DIVIDER    = 2.0f;                  // 1:2 external divider

enum Mode : uint8_t {
  MODE_OFF = 0,
  MODE_SLOW_DRIFT,   // slow, shallow — ambient
  MODE_FLUTTER,      // quick asymmetric flaps + pauses, most lifelike
  MODE_EXCITED,      // fast and full range
  MODE_BREATHE,      // very slow sine, like resting breath
  MODE_STARTLE,      // rare burst, otherwise still
  MODE_COUNT
};

static const char* MODE_NAMES[MODE_COUNT] = {
  "Off", "Slow Drift", "Flutter", "Excited", "Breathe", "Startle"
};

// Forward declaration: handleButton() triggers sleep before the definition below.
// The .ino preprocessor usually auto-prototypes, but don't rely on it.
void enterDeepSleep();

// ───────────────────────── state ─────────────────────────
Preferences prefs;
WebServer server(80);

RTC_DATA_ATTR uint8_t rtcMode = MODE_FLUTTER;   // survives deep sleep

struct {
  Mode     mode        = MODE_FLUTTER;
  float    speedScale  = 1.0f;   // 0.25 .. 3.0
  float    amplitude   = 1.0f;   // 0.10 .. 1.0
  bool     servoPowered = false;
  uint32_t lastFrameMs = 0;
  uint32_t lastActivityMs = 0;
  uint32_t phaseStartMs = 0;
  float    vbat        = 4.2f;
  bool     lowBattery  = false;
  bool     portalMode  = false;
  uint32_t settingsDirtyAt = 0;   // 0 = clean; else millis() of last change
} st;

// True while the button is STILL held from the boot-time portal gesture, so
// handleButton() can ignore it rather than reading it as a tap or long-press.
static bool btnHeldAtBoot = false;

// ───────────────────────── servo primitives ─────────────────────────

// pulse_us * 2^res / period_us. Multiply before divide; 32-bit math is required
// because pulse_us * 16384 overflows 16 bits.
static inline uint32_t usToDuty(uint16_t us) {
  return ((uint32_t)us * (1UL << LEDC_RES_BITS)) / 20000UL;
}

static void servoWriteUs(uint16_t us) {
  us = constrain(us, SERVO_MIN_US, SERVO_MAX_US);
  ledcWrite(PIN_SERVO, usToDuty(us));   // core 3.x: keyed by PIN, not channel
}

static void servoPower(bool on) {
  if (st.servoPowered == on) return;
  st.servoPowered = on;

  if (on) {
    digitalWrite(PIN_SERVO_EN, LOW);    // active-low load switch
    delay(8);                           // let the rail settle before the first pulse
    // Restart the stroke from the top. Without this, a resume after low-battery
    // recovery would snap the horn from rest to an arbitrary mid-stroke position
    // (a jerk through the linkage), and an ever-growing t eventually loses
    // precision in the generators' fmodf once it passes float's 24-bit mantissa.
    st.phaseStartMs = millis();
    servoWriteUs(WING_DOWN_US);
  } else {
    ledcWrite(PIN_SERVO, 0);            // stop pulsing so the horn goes limp
    digitalWrite(PIN_SERVO_EN, HIGH);
  }
}

// ───────────────────────── motion curves ─────────────────────────
//
// A linear sweep reads as robotic. Real wings snap up fast and fall slower,
// so each mode shapes the stroke with eased curves and irregular timing.

static float easeInOutCubic(float t) {
  return t < 0.5f ? 4.0f * t * t * t
                  : 1.0f - powf(-2.0f * t + 2.0f, 3.0f) / 2.0f;
}

static float easeOutQuad(float t) { return 1.0f - (1.0f - t) * (1.0f - t); }
static float easeInQuad(float t)  { return t * t; }

// Map normalised wing position [0..1] to a pulse width, honouring amplitude.
static uint16_t posToUs(float pos) {
  pos = constrain(pos, 0.0f, 1.0f);
  float span = (float)(WING_UP_US - WING_DOWN_US) * st.amplitude;
  return (uint16_t)((float)WING_DOWN_US + pos * span);
}

// Each generator returns wing position 0..1 for a given elapsed time.
// `period` values are pre-speed-scale.

static float genSlowDrift(uint32_t t) {
  const float period = 2600.0f / st.speedScale;
  float p = fmodf((float)t, period) / period;
  // gentle, mostly-shallow rise and fall
  return 0.15f + 0.55f * easeInOutCubic(p < 0.5f ? p * 2.0f : (1.0f - p) * 2.0f);
}

static float genFlutter(uint32_t t) {
  // Bursts of 3-5 quick flaps, then a pause. The pause length varies with the
  // burst index so it never sounds metronomic.
  const float flapMs  = 190.0f / st.speedScale;
  const float cycleMs = flapMs * 8.0f;
  float c = fmodf((float)t, cycleMs);
  uint32_t burst = (uint32_t)((float)t / cycleMs);
  uint8_t flaps = 3 + (burst % 3);     // 3,4,5 repeating

  if (c < flapMs * flaps) {
    float p = fmodf(c, flapMs) / flapMs;
    // fast up (snap), slower down (glide)
    return p < 0.35f ? easeOutQuad(p / 0.35f)
                     : 1.0f - easeInQuad((p - 0.35f) / 0.65f);
  }
  return 0.0f;   // rest between bursts
}

static float genExcited(uint32_t t) {
  const float period = 150.0f / st.speedScale;
  float p = fmodf((float)t, period) / period;
  return p < 0.4f ? easeOutQuad(p / 0.4f)
                  : 1.0f - easeInQuad((p - 0.4f) / 0.6f);
}

static float genBreathe(uint32_t t) {
  const float period = 4200.0f / st.speedScale;
  float p = fmodf((float)t, period) / period;
  return 0.10f + 0.35f * (0.5f - 0.5f * cosf(p * 2.0f * PI));
}

static float genStartle(uint32_t t) {
  // Still for ~6 s, then a short violent burst.
  const float cycleMs = 6500.0f / st.speedScale;
  const float burstMs = 900.0f;
  float c = fmodf((float)t, cycleMs);
  if (c > burstMs) return 0.0f;
  const float flapMs = 110.0f;
  float p = fmodf(c, flapMs) / flapMs;
  float decay = 1.0f - (c / burstMs);   // burst loses energy
  return decay * (p < 0.4f ? easeOutQuad(p / 0.4f)
                           : 1.0f - easeInQuad((p - 0.4f) / 0.6f));
}

static void updateMotion() {
  if (st.mode == MODE_OFF || st.lowBattery) {
    servoPower(false);
    return;
  }
  servoPower(true);

  uint32_t t = millis() - st.phaseStartMs;
  float pos = 0.0f;
  switch (st.mode) {
    case MODE_SLOW_DRIFT: pos = genSlowDrift(t); break;
    case MODE_FLUTTER:    pos = genFlutter(t);   break;
    case MODE_EXCITED:    pos = genExcited(t);   break;
    case MODE_BREATHE:    pos = genBreathe(t);   break;
    case MODE_STARTLE:    pos = genStartle(t);   break;
    default: break;
  }
  servoWriteUs(posToUs(pos));
}

// ───────────────────────── battery ─────────────────────────

static void readBattery() {
  // analogReadMilliVolts applies the factory eFuse calibration. Scaling a raw
  // analogRead by 3.3/4095 is wrong: full scale is ~2500 mV +/-10% at default
  // attenuation and the response is non-linear near the rail.
  // 16 samples averages out WiFi/BLE transmit spikes.
  uint32_t acc = 0;
  for (int i = 0; i < 16; i++) acc += analogReadMilliVolts(PIN_VBAT);
  st.vbat = VBAT_DIVIDER * (float)acc / 16.0f / 1000.0f;

  if (st.vbat < VBAT_CUTOFF_V && st.vbat > 2.5f) {   // >2.5V filters "no battery"
    if (!st.lowBattery) {
      Serial.printf("[batt] low: %.2f V — stopping servo\n", st.vbat);
      st.lowBattery = true;
    }
  } else if (st.vbat > VBAT_CUTOFF_V + 0.15f) {
    st.lowBattery = false;   // hysteresis so it doesn't chatter at the threshold
  }
}

// ───────────────────────── persistence ─────────────────────────

static void saveSettings() {
  prefs.begin("bfly", false);
  prefs.putUChar("mode", (uint8_t)st.mode);
  prefs.putFloat("speed", st.speedScale);
  prefs.putFloat("amp", st.amplitude);
  prefs.end();
}

static void loadSettings() {
  prefs.begin("bfly", true);
  st.mode       = (Mode)prefs.getUChar("mode", MODE_FLUTTER);
  st.speedScale = prefs.getFloat("speed", 1.0f);
  st.amplitude  = prefs.getFloat("amp", 1.0f);
  prefs.end();
  if (st.mode >= MODE_COUNT) st.mode = MODE_FLUTTER;
  st.speedScale = constrain(st.speedScale, 0.25f, 3.0f);
  st.amplitude  = constrain(st.amplitude, 0.10f, 1.0f);
}

static void setMode(Mode m) {
  st.mode = m;
  st.phaseStartMs = millis();
  st.lastActivityMs = millis();
  rtcMode = (uint8_t)m;
  Serial.printf("[mode] %s\n", MODE_NAMES[m]);
  saveSettings();
}

// ───────────────────────── button ─────────────────────────
//
// Tap = next mode, double tap = previous, hold 1.2s = deep sleep.

static void handleButton() {
  static bool     lastRaw   = HIGH;
  static bool     stable    = HIGH;
  static uint32_t lastEdge  = 0;
  static uint32_t pressedAt = 0;
  static uint32_t lastTapAt = 0;
  static uint8_t  pendingTaps = 0;
  static bool     longFired = false;

  // Swallow the boot-portal hold. setup() only needs 600ms to arm the portal,
  // but nobody releases at exactly 600ms — without this the still-held button
  // would latch a press here and fire deep sleep 1.2s into the portal session.
  if (btnHeldAtBoot) {
    if (digitalRead(PIN_BUTTON) == HIGH) btnHeldAtBoot = false;
    return;   // statics remain at their idle/HIGH init
  }

  bool raw = digitalRead(PIN_BUTTON);
  uint32_t now = millis();

  if (raw != lastRaw) { lastEdge = now; lastRaw = raw; }
  if (now - lastEdge > 25 && raw != stable) {      // 25 ms debounce
    stable = raw;
    if (stable == LOW) {                            // pressed
      pressedAt = now;
      longFired = false;
    } else {                                        // released
      if (!longFired && now - pressedAt < 700) {
        pendingTaps++;
        lastTapAt = now;
      }
    }
  }

  // long press while still held
  if (stable == LOW && !longFired && now - pressedAt > 1200) {
    longFired = true;
    pendingTaps = 0;
    enterDeepSleep();
  }

  // Resolve tap count once the double-tap window closes — but only while the
  // button is UP. Resolving mid-press would fire "next" for the first tap and
  // then count the release as another tap, so a slow double-tap would advance
  // two modes forward instead of going one back.
  if (pendingTaps > 0 && stable == HIGH && now - lastTapAt > 350) {
    if (pendingTaps == 1) {
      setMode((Mode)((st.mode + 1) % MODE_COUNT));
    } else {
      setMode((Mode)((st.mode + MODE_COUNT - 1) % MODE_COUNT));
    }
    pendingTaps = 0;
  }
}

// ───────────────────────── sleep ─────────────────────────

void enterDeepSleep() {
  Serial.println("[sleep] entering deep sleep");
  servoPower(false);
  digitalWrite(PIN_LED, LOW);
  saveSettings();

  // GPIO deep-sleep wake is LEVEL-triggered, not edge-triggered. We get here
  // from a long press, so the button is still down and GPIO is already LOW —
  // arming now would wake the chip instantly and loop forever. Wait for release.
  while (digitalRead(PIN_BUTTON) == LOW) delay(10);
  delay(50);   // debounce the release

  // ext0/ext1 do not exist on C3 (no ULP, RISC-V). GPIO wake takes a BITMASK —
  // 1ULL << pin, not the bare pin number. Only GPIO0-5 qualify.
  esp_deep_sleep_enable_gpio_wakeup(1ULL << PIN_BUTTON, ESP_GPIO_WAKEUP_GPIO_LOW);
  esp_deep_sleep_start();
}

// ───────────────────────── web UI ─────────────────────────

static const char INDEX_HTML[] PROGMEM = R"HTML(
<!DOCTYPE html><html><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Butterfly Wings</title><style>
:root{--bg:#12101a;--fg:#f4f1ff;--mut:#a79fc4;--accent:#c39bff;--card:#1e1a2e}
*{box-sizing:border-box}
body{margin:0;font:16px/1.5 system-ui,-apple-system,sans-serif;background:var(--bg);color:var(--fg);padding:20px;max-width:520px;margin:0 auto}
h1{font-size:1.35rem;margin:.2em 0 .1em}
p.sub{color:var(--mut);margin:0 0 1.4em;font-size:.9rem}
.grid{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-bottom:22px}
button.m{background:var(--card);color:var(--fg);border:1px solid #302a47;border-radius:14px;padding:16px 10px;font-size:1rem;cursor:pointer;transition:.15s}
button.m:hover{border-color:var(--accent)}
button.m.on{background:var(--accent);color:#1a1226;border-color:var(--accent);font-weight:600}
label{display:block;margin:18px 0 6px;color:var(--mut);font-size:.85rem;text-transform:uppercase;letter-spacing:.06em}
input[type=range]{width:100%;accent-color:var(--accent)}
.row{display:flex;justify-content:space-between;align-items:baseline}
.val{color:var(--accent);font-variant-numeric:tabular-nums;font-size:.9rem}
.bat{margin-top:26px;padding:14px;background:var(--card);border-radius:12px;display:flex;justify-content:space-between;align-items:center}
.warn{color:#ff8a8a}
</style></head><body>
<h1>Butterfly Wings</h1>
<p class="sub">swappable magnetic wings &middot; esp32-c3</p>
<div class="grid" id="modes"></div>
<div class="row"><label for="sp">Speed</label><span class="val" id="spv"></span></div>
<input type="range" id="sp" min="25" max="300" step="5">
<div class="row"><label for="am">Amplitude</label><span class="val" id="amv"></span></div>
<input type="range" id="am" min="10" max="100" step="5">
<div class="bat"><span>Battery</span><strong id="bat">--</strong></div>
<script>
const NAMES=["Off","Slow Drift","Flutter","Excited","Breathe","Startle"];
let cur=0;
const g=document.getElementById('modes');
NAMES.forEach((n,i)=>{const b=document.createElement('button');b.className='m';b.textContent=n;
  b.onclick=()=>fetch('/api/mode?m='+i).then(r=>r.json()).then(render);g.appendChild(b);});
function render(s){cur=s.mode;
  [...g.children].forEach((b,i)=>b.classList.toggle('on',i===cur));
  document.getElementById('sp').value=Math.round(s.speed*100);
  document.getElementById('am').value=Math.round(s.amp*100);
  document.getElementById('spv').textContent=s.speed.toFixed(2)+'x';
  document.getElementById('amv').textContent=Math.round(s.amp*100)+'%';
  const b=document.getElementById('bat');
  b.textContent=s.vbat.toFixed(2)+' V';
  b.className=s.low?'warn':'';
}
document.getElementById('sp').oninput=e=>{document.getElementById('spv').textContent=(e.target.value/100).toFixed(2)+'x';
  fetch('/api/set?speed='+e.target.value);};
document.getElementById('am').oninput=e=>{document.getElementById('amv').textContent=e.target.value+'%';
  fetch('/api/set?amp='+e.target.value);};
fetch('/api/state').then(r=>r.json()).then(render);
setInterval(()=>fetch('/api/state').then(r=>r.json()).then(s=>{
  const b=document.getElementById('bat');b.textContent=s.vbat.toFixed(2)+' V';b.className=s.low?'warn':'';}),5000);
</script></body></html>
)HTML";

static void sendState() {
  char buf[160];
  snprintf(buf, sizeof(buf),
           "{\"mode\":%u,\"speed\":%.2f,\"amp\":%.2f,\"vbat\":%.2f,\"low\":%s}",
           (unsigned)st.mode, st.speedScale, st.amplitude, st.vbat,
           st.lowBattery ? "true" : "false");
  server.send(200, "application/json", buf);
}

static void setupWeb() {
  server.on("/", HTTP_GET, [] {
    server.send_P(200, "text/html", INDEX_HTML);
  });
  server.on("/api/state", HTTP_GET, sendState);
  server.on("/api/mode", HTTP_GET, [] {
    if (server.hasArg("m")) {
      int m = server.arg("m").toInt();
      if (m >= 0 && m < MODE_COUNT) setMode((Mode)m);
    }
    sendState();
  });
  server.on("/api/set", HTTP_GET, [] {
    if (server.hasArg("speed"))
      st.speedScale = constrain(server.arg("speed").toFloat() / 100.0f, 0.25f, 3.0f);
    if (server.hasArg("amp"))
      st.amplitude = constrain(server.arg("amp").toFloat() / 100.0f, 0.10f, 1.0f);
    st.lastActivityMs = millis();
    // Don't write flash here — the UI fires on every slider input event, which
    // is ~56 requests per drag. Preferences::putFloat has no same-value
    // short-circuit, so each one would really hit flash. Defer and coalesce.
    st.settingsDirtyAt = millis();
    sendState();
  });
  server.onNotFound([] { server.send(404, "text/plain", "not found"); });
  server.begin();
}

// ───────────────────────── setup / loop ─────────────────────────

void setup() {
  Serial.begin(115200);
  delay(100);

  pinMode(PIN_SERVO_EN, OUTPUT);
  digitalWrite(PIN_SERVO_EN, HIGH);        // servo rail OFF until we're ready
  pinMode(PIN_BUTTON, INPUT_PULLUP);
  pinMode(PIN_LED, OUTPUT);
  digitalWrite(PIN_LED, LOW);

  // ledcAttach returns false if freq/resolution can't be realised — at 50 Hz
  // anything below 10 bits silently fails, so check it.
  if (!ledcAttach(PIN_SERVO, LEDC_FREQ_HZ, LEDC_RES_BITS)) {
    Serial.println("[fatal] ledcAttach failed — check freq/resolution");
    while (true) {                          // blink SOS rather than flail the servo
      digitalWrite(PIN_LED, !digitalRead(PIN_LED));
      delay(150);
    }
  }
  ledcWrite(PIN_SERVO, 0);

  loadSettings();

  esp_sleep_wakeup_cause_t cause = esp_sleep_get_wakeup_cause();
  if (cause == ESP_SLEEP_WAKEUP_GPIO) {     // C3 reports GPIO, not EXT0
    Serial.println("[boot] woke from button");
    st.mode = (Mode)(rtcMode < MODE_COUNT ? rtcMode : MODE_FLUTTER);
  }

  // Hold the button during boot to bring up the config portal.
  if (digitalRead(PIN_BUTTON) == LOW) {
    delay(600);
    if (digitalRead(PIN_BUTTON) == LOW) {
      st.portalMode = true;
      WiFi.mode(WIFI_AP);
      WiFi.softAP("butterfly-wings", "flutter123");
      Serial.printf("[wifi] AP up: http://%s/\n", WiFi.softAPIP().toString().c_str());
      setupWeb();
      digitalWrite(PIN_LED, HIGH);
    }
  }

  readBattery();
  st.phaseStartMs   = millis();
  st.lastActivityMs = millis();

  // If the button is still down as we leave setup(), tell handleButton() to
  // ignore it until released.
  btnHeldAtBoot = (digitalRead(PIN_BUTTON) == LOW);

  Serial.printf("[boot] mode=%s vbat=%.2fV\n", MODE_NAMES[st.mode], st.vbat);
}

void loop() {
  uint32_t now = millis();

  handleButton();
  if (st.portalMode) server.handleClient();

  if (now - st.lastFrameMs >= FRAME_INTERVAL_MS) {
    st.lastFrameMs = now;
    updateMotion();
  }

  static uint32_t lastBatt = 0;
  if (now - lastBatt > 5000) { lastBatt = now; readBattery(); }

  // Flush coalesced setting changes once the user stops dragging.
  if (st.settingsDirtyAt && now - st.settingsDirtyAt > 2000) {
    saveSettings();
    st.settingsDirtyAt = 0;
  }

  // Sleep on low battery — otherwise the servo stops but the ESP32 keeps running
  // and draining a cell that's already at its cutoff. Otherwise only auto-sleep
  // when genuinely idle; a running animation counts as activity.
  if (!st.portalMode &&
      (st.lowBattery ||
       (st.mode == MODE_OFF && now - st.lastActivityMs > IDLE_SLEEP_MS))) {
    enterDeepSleep();
  }

  delay(1);   // yield to idle/radio tasks — single-core C3 needs the breathing room
}
