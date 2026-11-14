// AI-assisted code (generated with Claude Code) — see README.
using System.Collections.Generic;
using System.Linq;
using LunarOutpost.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LunarOutpost.Game
{
    /// <summary>Builds the whole game at startup, runs the fixed-step loop, input, and the HUD.</summary>
    public sealed class GameController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);
            new GameObject("Game").AddComponent<GameController>();
        }

        Values _v;
        Simulation _sim;
        SimState _s;
        MoonWorld _world;
        PlayerRig _player;
        GameObject _lander;
        readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        System.Random _rand = new System.Random();

        double _acc;
        bool _fast, _userPaused, _helpOpen = true, _launchOpen, _clearedOpen;
        bool _flareModal;
        int _shelterHabitatId;

        bool _buildMode;
        FacilityKind _buildKind = FacilityKind.Solar;
        GameObject _ghost;
        FacilityKind? _ghostKind;

        int _oSupplies, _oMaterials, _oCrew, _oRelay;

        readonly List<(string text, float until)> _toasts = new List<(string, float)>();
        Font _font;
        Vector2 _helpScroll;

        static readonly FacilityKind[] BuildOrder =
        {
            FacilityKind.Habitat, FacilityKind.Solar, FacilityKind.Battery, FacilityKind.Antenna,
            FacilityKind.LifeSupport, FacilityKind.Greenhouse, FacilityKind.IceDrill, FacilityKind.Shield,
        };

        void Awake()
        {
            _v = Values.FromJson(Load("data/values"));
            var donki = DonkiEvent.ListFromJson(Load("data/donki-events"));
            I18n.Load("en");
            _font = Resources.Load<Font>("fonts/NotoSansJP");
            if (_font == null) throw new MissingReferenceException("Resources/fonts/NotoSansJP.ttf is missing");

            _world = new MoonWorld(_v.F("planet.sunElevationDeg"));
            _sim = new Simulation(_v, donki, () => _rand.NextDouble());
            var lp = new Vector3(MoonWorld.LanderPos.x, 0, MoonWorld.LanderPos.y);
            lp.y = _world.HeightAt(lp.x, lp.z);
            _lander = FacilityViews.Lander(_world, lp);
            _player = new PlayerRig(_world, _v.F("planet.gravity"), _v.F("player.walkSpeed"), _v.F("player.jumpSpeed"), lp + new Vector3(5, 0, -5));
            NewGame();
        }

        static string Load(string path)
        {
            var a = Resources.Load<TextAsset>(path);
            if (a == null) throw new MissingReferenceException($"Resources/{path}.json is missing");
            return a.text;
        }

        void NewGame()
        {
            foreach (var g in _views.Values) Destroy(g);
            _views.Clear();
            _s = _sim.NewGame(MoonWorld.LanderPos.x, MoonWorld.LanderPos.y);
            _s.PlayerLocation = PlayerLocation.Outside;
            _player.Teleport(_lander.transform.position + new Vector3(5, 0, -5));
            _player.SetVisible(true);
            _acc = 0;
            _flareModal = _launchOpen = _clearedOpen = false;
        }

        bool Paused => _userPaused || _helpOpen || _launchOpen || _flareModal || _clearedOpen || _s.Phase == Phase.GameOver;
        bool Outside => _s.PlayerLocation == PlayerLocation.Outside;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.hKey.wasPressedThisFrame) _helpOpen = !_helpOpen;
                if (kb.tKey.wasPressedThisFrame) I18n.Toggle();
                if (kb.pKey.wasPressedThisFrame) _userPaused = !_userPaused;
                if (kb.tabKey.wasPressedThisFrame) _fast = !_fast;
                if (kb.lKey.wasPressedThisFrame && _s.Phase != Phase.GameOver) _launchOpen = !_launchOpen;
                if (!Paused)
                {
                    if (kb.bKey.wasPressedThisFrame && Outside) _buildMode = !_buildMode;
                    if (kb.escapeKey.wasPressedThisFrame) _buildMode = false;
                    if (kb.eKey.wasPressedThisFrame) Interact();
                    if (kb.rKey.wasPressedThisFrame) CoverHabitat();
                    if (_buildMode)
                    {
                        for (int i = 0; i < BuildOrder.Length; i++)
                            if (kb[Key.Digit1 + i].wasPressedThisFrame) _buildKind = BuildOrder[i];
                    }
                }
            }
            if (!Outside) _buildMode = false;

            _player.Tick(Time.deltaTime, !Paused && Outside);

            if (!Paused)
            {
                _acc += Time.deltaTime * _v["time.gameHoursPerRealSecond"] * (_fast ? _v["time.fastMultiplier"] : 1);
                int guard = 0;
                while (_acc >= Simulation.StepHours && guard++ < 16)
                {
                    _acc -= Simulation.StepHours;
                    _s.PlayerX = _player.Position.x;
                    _s.PlayerZ = _player.Position.z;
                    Handle(_sim.Step(_s, _world));
                    if (Paused) { _acc = 0; break; }
                }
            }

            _world.UpdateSky(_s.SunAzimuthRad, _s.TimeHours, _v["time.sunPeriodDays"] * 24, _player.Cam.transform.position);
            var sunFlat = _world.SunDirection(_s.SunAzimuthRad);
            sunFlat.y = 0;
            foreach (var kv in _views)
            {
                var f = _s.Facilities.Find(x => x.Id == kv.Key);
                if (f != null && f.Kind == FacilityKind.Solar) kv.Value.transform.rotation = Quaternion.LookRotation(sunFlat);
            }
            UpdateBuildPreview();
        }

        // ---------- interaction ----------

        void Interact()
        {
            var p = _player.Position;
            if (!Outside)
            {
                Vector3 door = _s.PlayerLocation == PlayerLocation.Lander
                    ? _lander.transform.position
                    : _views.TryGetValue(_shelterHabitatId, out var h) ? h.transform.position : _lander.transform.position;
                _player.Teleport(door + new Vector3(0, 0, -5f));
                _player.SetVisible(true);
                _s.PlayerLocation = PlayerLocation.Outside;
                return;
            }
            if (Near(_lander.transform.position, 6f)) { EnterShelter(PlayerLocation.Lander, 0); return; }
            var hab = NearestHabitat(6f);
            if (hab != null) { EnterShelter(PlayerLocation.Habitat, hab.Id); return; }
            if (_world.IsIceAt(p.x, p.z)) Handle(_sim.ApplyCommand(_s, _world, new CollectIceCommand { X = p.x, Z = p.z }));
        }

        void EnterShelter(PlayerLocation loc, int habitatId)
        {
            _s.PlayerLocation = loc;
            _shelterHabitatId = habitatId;
            _player.SetVisible(false);
            Handle(_sim.ApplyCommand(_s, _world, new DeliverIceCommand()));
        }

        void CoverHabitat()
        {
            var hab = NearestHabitat(8f);
            if (hab != null && Outside) Handle(_sim.ApplyCommand(_s, _world, new CoverWithRegolithCommand { FacilityId = hab.Id }));
        }

        Facility NearestHabitat(float range)
        {
            return _s.Facilities.Where(f => f.Kind == FacilityKind.Habitat && Near(new Vector3(f.X, 0, f.Z), range))
                .OrderBy(f => (new Vector2(f.X, f.Z) - new Vector2(_player.Position.x, _player.Position.z)).sqrMagnitude)
                .FirstOrDefault();
        }

        bool Near(Vector3 target, float range) =>
            new Vector2(target.x - _player.Position.x, target.z - _player.Position.z).magnitude < range;

        // ---------- build ----------

        Vector3 BuildSpot()
        {
            var p = _player.Position + _player.Body.forward * 7f;
            p.y = _world.HeightAt(p.x, p.z);
            return p;
        }

        void UpdateBuildPreview()
        {
            if (!_buildMode)
            {
                if (_ghost != null) { Destroy(_ghost); _ghost = null; _ghostKind = null; }
                return;
            }
            if (_ghostKind != _buildKind)
            {
                if (_ghost != null) Destroy(_ghost);
                _ghost = FacilityViews.Create(_world, _buildKind, BuildSpot());
                _ghostKind = _buildKind;
            }
            var spot = BuildSpot();
            _ghost.transform.position = spot;
            _ghost.transform.rotation = Quaternion.LookRotation(_player.Body.forward);
            FacilityViews.Tint(_ghost, PreviewColor(spot));

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool place = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ||
                         (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (place && !Paused)
                Handle(_sim.ApplyCommand(_s, _world, new BuildCommand { Kind = _buildKind, X = spot.x, Z = spot.z }));
        }

        Color PreviewColor(Vector3 spot)
        {
            var bad = new Color(1f, 0.25f, 0.2f);
            var warn = new Color(1f, 0.85f, 0.2f);
            var ok = new Color(0.3f, 1f, 0.4f);
            if (_s.Materials < _sim.MaterialCost(_buildKind)) return bad;
            if (_buildKind == FacilityKind.IceDrill) return _world.IsIceAt(spot.x, spot.z) ? ok : bad;
            if (_buildKind == FacilityKind.Solar) return _world.IsSunlit(spot.x, spot.z, _s.SunAzimuthRad) ? ok : warn;
            return ok;
        }

        // ---------- events ----------

        void Handle(List<GameEvent> events)
        {
            foreach (var e in events)
            {
                switch (e.Type)
                {
                    case GameEventType.Built:
                    {
                        var f = _s.Facilities.Find(x => x.Id.ToString() == e.Detail);
                        var pos = new Vector3(f.X, _world.HeightAt(f.X, f.Z), f.Z);
                        var view = FacilityViews.Create(_world, f.Kind, pos);
                        view.transform.rotation = Quaternion.LookRotation(_player.Body.forward);
                        _views[f.Id] = view;
                        Toast(I18n.T("msg.built", I18n.T("fac." + Simulation.Key(f.Kind))));
                        break;
                    }
                    case GameEventType.BuildFailed:
                    case GameEventType.FacilityStopped:
                    case GameEventType.LaunchFailed:
                        Toast(I18n.T(e.Detail));
                        break;
                    case GameEventType.PlayerDowned:
                        _player.Teleport(_lander.transform.position + new Vector3(0, 0, -5f));
                        _player.SetVisible(false);
                        _shelterHabitatId = 0;
                        Toast(I18n.T(e.Detail), 8f);
                        break;
                    case GameEventType.EventStarted:
                        if (_s.ActiveEvent.Alerted) _flareModal = true;
                        else Toast(I18n.T("flare.noAlert"), 8f);
                        break;
                    case GameEventType.EventEnded: Toast(I18n.T("flare.ended")); break;
                    case GameEventType.LaunchOrdered: Toast(I18n.T("msg.launchOrdered")); break;
                    case GameEventType.LaunchArrived: Toast(I18n.T("msg.launchArrived")); break;
                    case GameEventType.CommLost: Toast(I18n.T("msg.commLost")); break;
                    case GameEventType.CommRestored: Toast(I18n.T("msg.commRestored")); break;
                    case GameEventType.IceCollected: Toast(I18n.T("msg.iceCollected")); break;
                    case GameEventType.IceDelivered: Toast(I18n.T("msg.iceDelivered")); break;
                    case GameEventType.RegolithCovered: Toast(I18n.T("msg.regolith")); break;
                    case GameEventType.Cleared: _clearedOpen = true; break;
                }
            }
        }

        void Toast(string text, float seconds = 5f)
        {
            _toasts.Add((text, Time.unscaledTime + seconds));
            if (_toasts.Count > 5) _toasts.RemoveAt(0);
        }

        // ---------- HUD ----------

        GUIStyle _label, _small, _big, _box, _button;
        float _uiScale;

        void Styles()
        {
            GUI.skin.font = _font;
            _uiScale = Mathf.Max(0.6f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * _uiScale);
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 17, wordWrap = true };
            _label.normal.textColor = Color.white;
            _small = new GUIStyle(_label) { fontSize = 14 };
            _small.normal.textColor = new Color(0.8f, 0.8f, 0.85f);
            _big = new GUIStyle(_label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _box = new GUIStyle(GUI.skin.box) { font = _font };
            _button = new GUIStyle(GUI.skin.button) { font = _font, fontSize = 17 };
        }

        float W => Screen.width / _uiScale;
        float H => Screen.height / _uiScale;

        static readonly Color Ok = new Color(0.45f, 1f, 0.55f);
        static readonly Color Warn = new Color(1f, 0.8f, 0.3f);
        static readonly Color Bad = new Color(1f, 0.4f, 0.35f);

        void Line(ref float y, string text, Color? color = null, GUIStyle style = null)
        {
            style = style ?? _label;
            var prev = style.normal.textColor;
            if (color.HasValue) style.normal.textColor = color.Value;
            float h = style.CalcHeight(new GUIContent(text), 440);
            GUI.Label(new Rect(18, y, 440, h), text, style);
            style.normal.textColor = prev;
            y += h + 1;
        }

        void Bar(ref float y, string label, double value, double max)
        {
            float frac = Mathf.Clamp01((float)(value / max));
            GUI.Label(new Rect(18, y, 150, 22), label, _small);
            GUI.Box(new Rect(150, y + 4, 220, 14), GUIContent.none);
            var c = frac >= 0.6f ? Ok : frac >= 0.3f ? Warn : Bad;
            var tex = Texture2D.whiteTexture;
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(151, y + 5, 218 * frac, 12), tex);
            GUI.color = prev;
            GUI.Label(new Rect(378, y, 80, 22), value.ToString("0"), _small);
            y += 22;
        }

        void OnGUI()
        {
            Styles();
            var prevColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(8, 8, 460, 660), Texture2D.whiteTexture);
            GUI.color = prevColor;

            float y = 14;
            int day = (int)(_s.TimeHours / 24) + 1;
            Line(ref y, $"{I18n.T("title")}   {I18n.T("hud.day", day)}   {I18n.T("hud.speed", _fast ? _v["time.fastMultiplier"] : 1)}" +
                        (Paused ? "   " + I18n.T("hud.paused") : ""), null, _big);

            double life = _sim.LifeDays(_s);
            Line(ref y, I18n.T("hud.life", life.ToString("0.0")), life >= 7 ? Ok : life >= 3 ? Warn : Bad);
            Line(ref y, I18n.T("hud.lifeDetail", _s.OxygenKg.ToString("0.0"), _s.WaterKg.ToString("0.0"), _s.FoodKg.ToString("0.0"), _s.IceKg.ToString("0.0")), null, _small);

            Line(ref y, I18n.T("hud.power", _s.PowerProducedKw.ToString("0.0"), _s.PowerDemandKw.ToString("0.0")),
                _s.PowerProducedKw >= _s.PowerDemandKw ? Ok : _s.BatteryKwh > 0 ? Warn : Bad);
            Line(ref y, I18n.T("hud.battery", _s.BatteryKwh.ToString("0"), _sim.BatteryCapacity(_s).ToString("0")), null, _small);

            Line(ref y, I18n.T("hud.shield", _s.PlayerDoseMsv.ToString("0.0"), _v["rad.doseLimitMsv"].ToString("0")));
            Line(ref y, I18n.T("hud.shieldBase", _sim.IndoorFactor(_s, _sim.CrewShelter(_s)).ToString("0.00")), null, _small);

            if (Outside)
            {
                double tank = _s.TankHours / _v["player.tankHours"];
                Line(ref y, I18n.T("hud.tank", _s.TankHours.ToString("0")), tank > 0.5 ? Ok : tank > 0.25 ? Warn : Bad);
            }
            else
            {
                Line(ref y, I18n.T("hud.indoors", I18n.T(_s.PlayerLocation == PlayerLocation.Lander ? "loc.lander" : "loc.habitat")), Ok);
            }

            Line(ref y, _s.CommLinked ? I18n.T("hud.commOn", _v["comm.delaySec"].ToString("0.0")) : I18n.T("hud.commOff"), _s.CommLinked ? Ok : Bad);
            Line(ref y, $"{I18n.T(_s.EarthVisible ? "hud.earthUp" : "hud.earthDown")}  ·  {I18n.T("hud.relays", _s.RelaySatellites)}", null, _small);
            Line(ref y, $"{I18n.T("hud.materials", _s.Materials.ToString("0"))}   {I18n.T("hud.crew", _s.CrewCount, _sim.CrewCapacity(_s))}" +
                        (_s.CarriedIceKg > 0 ? "   " + I18n.T("hud.carry", _s.CarriedIceKg.ToString("0")) : ""), null, _small);
            Line(ref y, _s.TimeHours >= _s.NextLaunchAtHours ? I18n.T("hud.launchReady")
                : I18n.T("hud.launchIn", ((_s.NextLaunchAtHours - _s.TimeHours) / 24).ToString("0.0")), null, _small);
            foreach (var l in _s.Launches)
                Line(ref y, I18n.T("hud.inTransit", ((l.ArrivesAtHours - _s.TimeHours) / 24).ToString("0.0")), null, _small);

            y += 6;
            Line(ref y, I18n.T("hud.dev", _s.Dev.Total.ToString("0"), _s.Dev.Best.ToString("0")), _s.Dev.Total >= _v["dev.clearThreshold"] ? Ok : null);
            Bar(ref y, I18n.T("hud.devSelf"), _s.Dev.SelfSufficiency, 100);
            Bar(ref y, I18n.T("hud.devStab"), _s.Dev.Stability, 100);
            Bar(ref y, I18n.T("hud.devSafe"), _s.Dev.Safety, 100);
            Bar(ref y, I18n.T("hud.devScale"), _s.Dev.Scale, 100);

            if (_s.ActiveEvent != null)
            {
                var src = _s.ActiveEvent.Source;
                var r = new Rect(W / 2 - 360, 12, 720, 40);
                GUI.color = new Color(0.6f, 0, 0, 0.75f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = prevColor;
                GUI.Label(new Rect(r.x + 12, r.y + 8, r.width - 24, 30),
                    I18n.T("hud.flare", src.Kind == "sep" ? "SEP" : "Flare", src.ClassOrIntensity, src.Date), _label);
            }

            DrawContextHints();
            DrawToasts();
            GUI.Label(new Rect(14, H - 30, W - 28, 26), I18n.T("hud.controls"), _small);

            if (_buildMode) DrawBuildMenu();
            if (_launchOpen) DrawLaunchMenu();
            if (_flareModal) DrawFlareModal();
            if (_clearedOpen) DrawCleared();
            if (_s.Phase == Phase.GameOver) DrawGameOver();
            if (_helpOpen) DrawHelp();
        }

        void DrawContextHints()
        {
            var hints = new List<string>();
            var p = _player.Position;
            if (!Outside) hints.Add(I18n.T("act.exit"));
            else
            {
                if (Near(_lander.transform.position, 6f)) hints.Add(I18n.T("act.enter", I18n.T("loc.lander")));
                else if (NearestHabitat(6f) != null) hints.Add(I18n.T("act.enter", I18n.T("loc.habitat")));
                else if (_world.IsIceAt(p.x, p.z) && _s.CarriedIceKg <= 0) hints.Add(I18n.T("act.collect"));
                if (_s.CarriedIceKg > 0) hints.Add(I18n.T("act.deliver"));
                var hab = NearestHabitat(8f);
                if (hab != null && !hab.RegolithCover) hints.Add(I18n.T("act.cover"));
            }
            float y = H - 70 - hints.Count * 26;
            foreach (var h in hints)
            {
                GUI.Label(new Rect(W / 2 - 300, y, 600, 26), h, new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter });
                y += 26;
            }
        }

        void DrawToasts()
        {
            _toasts.RemoveAll(t => t.until < Time.unscaledTime);
            float y = 70;
            foreach (var t in _toasts)
            {
                var r = new Rect(W - 470, y, 455, 0);
                r.height = _label.CalcHeight(new GUIContent(t.text), 435) + 12;
                var prev = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.6f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = prev;
                GUI.Label(new Rect(r.x + 10, r.y + 6, 435, r.height), t.text, _label);
                y += r.height + 6;
            }
        }

        Rect Panel(float w, float h)
        {
            var r = new Rect(W / 2 - w / 2, H / 2 - h / 2, w, h);
            var prev = GUI.color;
            GUI.color = new Color(0.02f, 0.03f, 0.06f, 0.92f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
            return r;
        }

        void DrawBuildMenu()
        {
            float w = 470, h = 60 + BuildOrder.Length * 46 + 30;
            var r = new Rect(W - w - 12, H - h - 40, w, h);
            var prev = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
            float y = r.y + 8;
            GUI.Label(new Rect(r.x + 10, y, w - 20, 44), I18n.T("build.title"), _small);
            y += 44;
            for (int i = 0; i < BuildOrder.Length; i++)
            {
                var k = BuildOrder[i];
                string key = Simulation.Key(k);
                var st = new GUIStyle(_label);
                st.normal.textColor = k == _buildKind ? Ok : Color.white;
                GUI.Label(new Rect(r.x + 10, y, w - 20, 22), I18n.T("build.item", i + 1, I18n.T("fac." + key), _sim.MaterialCost(k).ToString("0")), st);
                GUI.Label(new Rect(r.x + 30, y + 21, w - 40, 22), I18n.T("facdesc." + key), _small);
                y += 46;
            }
            var spot = BuildSpot();
            string cond = _buildKind == FacilityKind.IceDrill
                ? I18n.T(_world.IsIceAt(spot.x, spot.z) ? "build.ice" : "build.noIce")
                : I18n.T(_world.IsSunlit(spot.x, spot.z, _s.SunAzimuthRad) ? "build.sunlit" : "build.shadow");
            GUI.Label(new Rect(r.x + 10, y, w - 20, 24), $"{I18n.T("build.preview", I18n.T("fac." + Simulation.Key(_buildKind)))} — {cond}", _label);
        }

        void DrawLaunchMenu()
        {
            var r = Panel(640, 470);
            float y = r.y + 14;
            GUI.Label(new Rect(r.x + 20, y, 600, 34), I18n.T("launch.title"), _big); y += 40;
            GUI.Label(new Rect(r.x + 20, y, 600, 44), I18n.T("launch.costHelp"), _small); y += 44;

            OrderRow(ref y, r, "launch.supplies", "supplies", ref _oSupplies);
            OrderRow(ref y, r, "launch.materials", "materials", ref _oMaterials);
            OrderRow(ref y, r, "launch.crew", "crew", ref _oCrew);
            OrderRow(ref y, r, "launch.relay", "relay", ref _oRelay);

            double cost = _sim.LaunchCost(_oSupplies, _oMaterials, _oCrew, _oRelay);
            double cap = _v["launch.capKg"];
            y += 6;
            GUI.Label(new Rect(r.x + 20, y, 600, 24), I18n.T("launch.cost", cost.ToString("0"), cap.ToString("0")), _label); y += 26;
            GUI.Box(new Rect(r.x + 20, y, 600, 18), GUIContent.none);
            var prev = GUI.color;
            GUI.color = cost <= cap ? Ok : Bad;
            GUI.DrawTexture(new Rect(r.x + 21, y + 1, 598 * Mathf.Clamp01((float)(cost / cap)), 16), Texture2D.whiteTexture);
            GUI.color = prev;
            y += 26;
            GUI.Label(new Rect(r.x + 20, y, 600, 24), I18n.T("launch.arrive", _v["launch.transitDays"], _v["launch.intervalDays"]), _small); y += 26;

            string err = _sim.CanOrderLaunch(_s, _oSupplies, _oMaterials, _oCrew, _oRelay);
            if (err != null) GUI.Label(new Rect(r.x + 20, y, 600, 24), I18n.T(err), new GUIStyle(_label) { normal = { textColor = Bad } });
            y += 30;
            GUI.enabled = err == null;
            if (GUI.Button(new Rect(r.x + 20, y, 200, 36), I18n.T("launch.order"), _button))
            {
                Handle(_sim.ApplyCommand(_s, _world, new OrderLaunchCommand { Supplies = _oSupplies, Materials = _oMaterials, Crew = _oCrew, Relay = _oRelay }));
                _oSupplies = _oMaterials = _oCrew = _oRelay = 0;
                _launchOpen = false;
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(r.x + 240, y, 200, 36), I18n.T("launch.close"), _button)) _launchOpen = false;
        }

        void OrderRow(ref float y, Rect r, string labelKey, string item, ref int count)
        {
            GUI.Label(new Rect(r.x + 20, y, 420, 24), I18n.T(labelKey), _label);
            GUI.Label(new Rect(r.x + 20, y + 22, 420, 20), I18n.T("launch.each", Simulation.CostOf(_v, item).ToString("0")), _small);
            if (GUI.Button(new Rect(r.x + 460, y + 4, 36, 32), "-", _button) && count > 0) count--;
            GUI.Label(new Rect(r.x + 505, y + 6, 40, 30), count.ToString(), _label);
            if (GUI.Button(new Rect(r.x + 540, y + 4, 36, 32), "+", _button)) count++;
            y += 48;
        }

        void DrawFlareModal()
        {
            var src = _s.ActiveEvent?.Source;
            if (src == null) { _flareModal = false; return; }
            var r = Panel(620, 300);
            GUI.Label(new Rect(r.x + 20, r.y + 16, 580, 36), I18n.T("flare.title"), new GUIStyle(_big) { normal = { textColor = Bad } });
            GUI.Label(new Rect(r.x + 20, r.y + 60, 580, 150),
                I18n.T("flare.body", src.Kind == "sep" ? "SEP" : "X-class flare", src.ClassOrIntensity, src.Date, src.DonkiId), _label);
            if (GUI.Button(new Rect(r.x + 20, r.y + 240, 270, 40), I18n.T("flare.shelter"), _button))
            {
                var hab = _s.Facilities.FirstOrDefault(f => f.Kind == FacilityKind.Habitat);
                EnterShelter(hab != null ? PlayerLocation.Habitat : PlayerLocation.Lander, hab?.Id ?? 0);
                _flareModal = false;
            }
            if (GUI.Button(new Rect(r.x + 320, r.y + 240, 270, 40), I18n.T("flare.continue"), _button)) _flareModal = false;
        }

        void DrawCleared()
        {
            var r = Panel(600, 200);
            GUI.Label(new Rect(r.x + 20, r.y + 20, 560, 120), I18n.T("msg.cleared"), new GUIStyle(_big) { wordWrap = true, normal = { textColor = Ok } });
            if (GUI.Button(new Rect(r.x + 200, r.y + 145, 200, 38), "OK", _button)) _clearedOpen = false;
        }

        void DrawGameOver()
        {
            var r = Panel(600, 260);
            GUI.Label(new Rect(r.x + 20, r.y + 20, 560, 40), I18n.T("over.title"), new GUIStyle(_big) { normal = { textColor = Bad } });
            string reason = _s.OxygenKg <= 0 ? "over.oxygen" : _s.WaterKg <= 0 ? "over.water" : "over.food";
            GUI.Label(new Rect(r.x + 20, r.y + 75, 560, 30), I18n.T(reason), _label);
            GUI.Label(new Rect(r.x + 20, r.y + 115, 560, 30), I18n.T("over.stats", _s.Dev.Best.ToString("0"), _s.DaysSurvived), _label);
            if (GUI.Button(new Rect(r.x + 200, r.y + 190, 200, 40), I18n.T("over.restart"), _button)) NewGame();
        }

        void DrawHelp()
        {
            var r = Panel(Mathf.Min(760, W - 40), Mathf.Min(640, H - 40));
            GUI.Label(new Rect(r.x + 20, r.y + 14, r.width - 40, 36), I18n.T("help.title"), _big);
            var sources = _v.Sources.Values.Where(s => !s.StartsWith("TBD") && !s.StartsWith("SPEC")).Distinct().ToList();
            sources.Add("NASA DONKI (CCMC) — archived flare/SEP events");
            string text = I18n.T("help.body") + "\n\n" + I18n.T("help.sources") + "\n• " + string.Join("\n• ", sources);
            float inner = r.width - 60;
            float th = _label.CalcHeight(new GUIContent(text), inner);
            _helpScroll = GUI.BeginScrollView(new Rect(r.x + 20, r.y + 56, r.width - 30, r.height - 120), _helpScroll, new Rect(0, 0, inner, th));
            GUI.Label(new Rect(0, 0, inner, th), text, _label);
            GUI.EndScrollView();
            if (GUI.Button(new Rect(r.x + r.width / 2 - 110, r.y + r.height - 54, 220, 40), I18n.T("help.close"), _button)) _helpOpen = false;
        }
    }
}
