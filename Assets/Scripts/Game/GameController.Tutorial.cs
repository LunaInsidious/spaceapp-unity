// AI-assisted code (generated with Claude Code) — see README.
using System;
using System.Collections.Generic;
using LunarOutpost.Sim;
using UnityEngine;

namespace LunarOutpost.Game
{
    /// <summary>
    /// Interactive tutorial: three short missions (power, radiation, ice &amp; oxygen).
    /// Each step is a learning card, a quiz, or a task with a world marker. HUD parts appear as they are learned.
    /// </summary>
    public sealed partial class GameController
    {
        enum StepKind { Card, Quiz, Task }

        sealed class TStep
        {
            public StepKind Kind;
            public int Mission;                 // 1..3, 0 = intro/outro
            public string Text;                 // i18n key: card body / quiz question / task instruction
            public string[] Options;            // quiz: i18n keys
            public int Answer;                  // quiz: correct option index
            public string Explain;              // quiz: i18n key shown after the right answer
            public Func<bool> Done;             // task completion
            public Func<Vector3?> Target;       // task marker
            public Action OnEnter;
            public string[] Reveal;             // HUD sections that appear from this step on
        }

        List<TStep> _steps;
        int _step = -1;
        bool _tutorialDone;
        readonly HashSet<string> _hud = new HashSet<string>();
        int _quizWrong = -1;
        bool _quizSolved;
        string _flareChoice;
        GameObject _beacon;
        Vector3? _sunnySpot, _shadowSpot;
        float _spotRefreshAt;

        bool TutorialModal => !_tutorialDone && _step >= 0 && _steps[_step].Kind != StepKind.Task;
        bool ShowHud(string section) => _tutorialDone || _hud.Contains(section);
        float TutorialTimeScale => _tutorialDone ? 1f : _v.F("tutorial.timeScale");

        void StartTutorial()
        {
            _tutorialDone = false;
            _sim.RandomEvents = false;
            _hud.Clear();
            _flareChoice = null;
            _sunnySpot = _shadowSpot = null;
            _steps = new List<TStep>
            {
                new TStep { Kind = StepKind.Card, Text = "tut.intro", Reveal = new[] { "life" } },

                // Mission 1: power
                new TStep { Kind = StepKind.Card, Mission = 1, Text = "tut.m1.intro", Reveal = new[] { "power" } },
                new TStep { Kind = StepKind.Task, Mission = 1, Text = "tut.m1.walkSun", Target = () => _sunnySpot,
                            Done = () => _sunnySpot.HasValue && Near(_sunnySpot.Value, 6f) },
                new TStep { Kind = StepKind.Task, Mission = 1, Text = "tut.m1.buildSolar", Reveal = new[] { "materials" },
                            Done = () => _s.Count(FacilityKind.Solar) > 0 && _s.PowerProducedKw > 0 },
                new TStep { Kind = StepKind.Card, Mission = 1, Text = "tut.m1.solarLearn" },
                new TStep { Kind = StepKind.Task, Mission = 1, Text = "tut.m1.walkShadow", Target = () => _shadowSpot,
                            Done = () => _shadowSpot.HasValue && Near(_shadowSpot.Value, 6f) },
                new TStep { Kind = StepKind.Quiz, Mission = 1, Text = "tut.m1.quiz", Options = new[] { "tut.m1.quizA", "tut.m1.quizB", "tut.m1.quizC" },
                            Answer = 1, Explain = "tut.m1.quizExplain" },
                new TStep { Kind = StepKind.Task, Mission = 1, Text = "tut.m1.buildBattery", Reveal = new[] { "battery" },
                            Done = () => _s.Count(FacilityKind.Battery) > 0 },
                new TStep { Kind = StepKind.Card, Mission = 1, Text = "tut.m1.batteryLearn" },

                // Mission 2: radiation
                new TStep { Kind = StepKind.Card, Mission = 2, Text = "tut.m2.intro", Reveal = new[] { "shield" } },
                new TStep { Kind = StepKind.Quiz, Mission = 2, Text = "tut.m2.quiz", Options = new[] { "tut.m2.quizA", "tut.m2.quizB", "tut.m2.quizC" },
                            Answer = 2, Explain = "tut.m2.quizExplain" },
                new TStep { Kind = StepKind.Task, Mission = 2, Text = "tut.m2.buildAntenna", Reveal = new[] { "comm" },
                            Done = () => _s.Count(FacilityKind.Antenna) > 0 && _s.CommLinked },
                new TStep { Kind = StepKind.Card, Mission = 2, Text = "tut.m2.antennaLearn" },
                new TStep { Kind = StepKind.Task, Mission = 2, Text = "tut.m2.storm",
                            OnEnter = () => { _flareChoice = null; Handle(_sim.StartArchivedEvent(_s, 0)); },
                            Done = () => _flareChoice != null },
                new TStep { Kind = StepKind.Card, Mission = 2, Text = "tut.m2.stormResult" },
                new TStep { Kind = StepKind.Task, Mission = 2, Text = "tut.m2.waitStorm",
                            Done = () => _s.ActiveEvent == null && Outside },

                // Mission 3: ice and oxygen
                new TStep { Kind = StepKind.Card, Mission = 3, Text = "tut.m3.intro", Reveal = new[] { "tank" } },
                new TStep { Kind = StepKind.Task, Mission = 3, Text = "tut.m3.goCrater", Target = () => CraterTarget(),
                            Done = () => _world.IsIceAt(_player.Position.x, _player.Position.z) },
                new TStep { Kind = StepKind.Card, Mission = 3, Text = "tut.m3.craterLearn" },
                new TStep { Kind = StepKind.Task, Mission = 3, Text = "tut.m3.collect", Target = () => CraterTarget(),
                            Done = () => _s.CarriedIceKg > 0 },
                new TStep { Kind = StepKind.Task, Mission = 3, Text = "tut.m3.bringBack", Target = () => _lander.transform.position,
                            Done = () => _s.IceKg > 0 },
                new TStep { Kind = StepKind.Task, Mission = 3, Text = "tut.m3.buildLifeSupport",
                            Done = () => _s.Count(FacilityKind.LifeSupport) > 0 },
                new TStep { Kind = StepKind.Quiz, Mission = 3, Text = "tut.m3.quiz", Options = new[] { "tut.m3.quizA", "tut.m3.quizB", "tut.m3.quizC" },
                            Answer = 0, Explain = "tut.m3.quizExplain" },

                new TStep { Kind = StepKind.Card, Text = "tut.done", Reveal = new[] { "dev", "launch" } },
            };
            EnterStep(0);
        }

        Vector3? CraterTarget() => new Vector3(MoonWorld.CraterCenter.x, 0, MoonWorld.CraterCenter.y);

        void EnterStep(int i)
        {
            _step = i;
            _quizWrong = -1;
            _quizSolved = false;
            if (i >= _steps.Count) { FinishTutorial(); return; }
            var st = _steps[i];
            if (st.Reveal != null) foreach (var r in st.Reveal) _hud.Add(r);
            st.OnEnter?.Invoke();
        }

        void FinishTutorial()
        {
            _tutorialDone = true;
            _sim.RandomEvents = true;
            _step = -1;
            if (_beacon != null) _beacon.SetActive(false);
        }

        void SkipTutorial()
        {
            FinishTutorial();
            if (_s.ActiveEvent != null && _flareChoice == null) _flareModal = false;
        }

        void TickTutorial()
        {
            if (_tutorialDone || _step < 0) return;
            if (Time.unscaledTime >= _spotRefreshAt)
            {
                _spotRefreshAt = Time.unscaledTime + 2f;
                _sunnySpot = FindSpot(true, _sunnySpot);
                _shadowSpot = FindSpot(false, _shadowSpot);
            }
            var st = _steps[_step];
            Vector3? target = st.Kind == StepKind.Task ? st.Target?.Invoke() : null;
            UpdateBeacon(target);
            if (st.Kind == StepKind.Task && st.Done()) EnterStep(_step + 1);
        }

        /// <summary>Nearest spot to the lander that is (or is not) in sunlight now. Keeps the old one while it still fits.</summary>
        Vector3? FindSpot(bool sunlit, Vector3? keep)
        {
            if (keep.HasValue && _world.IsSunlit(keep.Value.x, keep.Value.z, _s.SunAzimuthRad) == sunlit) return keep;
            var origin = _lander.transform.position;
            for (float r = 18f; r <= 150f; r += 6f)
            for (int a = 0; a < 24; a++)
            {
                float ang = a * Mathf.PI * 2 / 24;
                float x = origin.x + Mathf.Cos(ang) * r, z = origin.z + Mathf.Sin(ang) * r;
                if (_world.IsIceAt(x, z)) continue;
                if (_world.IsSunlit(x, z, _s.SunAzimuthRad) == sunlit) return new Vector3(x, 0, z);
            }
            return null;
        }

        void UpdateBeacon(Vector3? target)
        {
            if (_beacon == null)
            {
                _beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _beacon.name = "TutorialBeacon";
                Destroy(_beacon.GetComponent<Collider>());
                _beacon.transform.localScale = new Vector3(1.2f, 25f, 1.2f);
                var r = _beacon.GetComponent<Renderer>();
                r.sharedMaterial = MoonWorld.UnlitMat(new Color(0.3f, 1f, 0.9f));
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _beacon.SetActive(target.HasValue);
            if (!target.HasValue) return;
            var t = target.Value;
            _beacon.transform.position = new Vector3(t.x, _world.HeightAt(t.x, t.z) + 25f, t.z);
        }

        // ---------- tutorial UI ----------

        void DrawTutorial()
        {
            if (_tutorialDone || _step < 0) return;
            var st = _steps[_step];
            string header = st.Mission > 0 ? I18n.T("tut.mission", st.Mission, I18n.T($"tut.m{st.Mission}.name")) : I18n.T("tut.title");

            if (st.Kind == StepKind.Task)
            {
                if (_flareModal) return;   // the alert itself is the instruction
                // Instruction banner at the top centre, big and short.
                float w = Mathf.Min(760, W - 500), x = 480 + (W - 480 - w) / 2;
                var body = new GUIStyle(_label) { fontSize = 20, wordWrap = true };
                float h = body.CalcHeight(new GUIContent(I18n.T(st.Text)), w - 32) + 58;
                var r = new Rect(x, 64, w, h);
                Fill(r, new Color(0.05f, 0.25f, 0.35f, 0.92f));
                GUI.Label(new Rect(r.x + 16, r.y + 8, w - 140, 26), header, new GUIStyle(_small) { normal = { textColor = new Color(0.6f, 1f, 0.95f) } });
                GUI.Label(new Rect(r.x + 16, r.y + 34, w - 32, h - 40), I18n.T(st.Text), body);
                if (GUI.Button(new Rect(r.xMax - 120, r.y + 6, 110, 26), I18n.T("tut.skip"), new GUIStyle(_button) { fontSize = 13 })) SkipTutorial();
                DrawTargetArrow(st.Target?.Invoke());
                return;
            }

            var p = Panel(Mathf.Min(700, W - 40), 0 + Mathf.Min(460, H - 40));
            float y = p.y + 16;
            GUI.Label(new Rect(p.x + 24, y, p.width - 48, 28), header, new GUIStyle(_small) { fontSize = 16, normal = { textColor = new Color(0.6f, 1f, 0.95f) } });
            y += 32;
            var text = new GUIStyle(_label) { fontSize = 20, wordWrap = true };
            string bodyText = st.Text == "tut.m2.stormResult" ? StormResultText() : I18n.T(st.Text);
            float th = text.CalcHeight(new GUIContent(bodyText), p.width - 48);
            GUI.Label(new Rect(p.x + 24, y, p.width - 48, th), bodyText, text);
            y += th + 14;

            if (st.Kind == StepKind.Card)
            {
                if (GUI.Button(new Rect(p.x + p.width / 2 - 120, p.yMax - 60, 240, 44), I18n.T(_step == _steps.Count - 1 ? "tut.start" : "tut.next"), _button))
                    EnterStep(_step + 1);
            }
            else if (!_quizSolved)
            {
                for (int i = 0; i < st.Options.Length; i++)
                {
                    var bs = new GUIStyle(_button) { fontSize = 18, wordWrap = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 6, 6) };
                    if (_quizWrong == i) bs.normal.textColor = Bad;
                    if (GUI.Button(new Rect(p.x + 24, y, p.width - 48, 44), $"{(char)('A' + i)}.  {I18n.T(st.Options[i])}", bs))
                    {
                        if (i == st.Answer) _quizSolved = true;
                        else _quizWrong = i;
                    }
                    y += 50;
                }
                if (_quizWrong >= 0)
                    GUI.Label(new Rect(p.x + 24, y + 4, p.width - 48, 50), I18n.T("tut.tryAgain"), new GUIStyle(_label) { normal = { textColor = Warn } });
            }
            else
            {
                GUI.Label(new Rect(p.x + 24, y, p.width - 48, 30), I18n.T("tut.correct"), new GUIStyle(_big) { normal = { textColor = Ok } });
                y += 36;
                GUI.Label(new Rect(p.x + 24, y, p.width - 48, p.yMax - y - 70), I18n.T(st.Explain), new GUIStyle(_label) { fontSize = 18, wordWrap = true });
                if (GUI.Button(new Rect(p.x + p.width / 2 - 120, p.yMax - 60, 240, 44), I18n.T("tut.next"), _button)) EnterStep(_step + 1);
            }
            if (GUI.Button(new Rect(p.xMax - 120, p.y + 10, 110, 26), I18n.T("tut.skip"), new GUIStyle(_button) { fontSize = 13 })) SkipTutorial();
        }

        string StormResultText()
        {
            double outside = 100;
            double lander = _sim.IndoorFactor(_s, PlayerLocation.Lander) * 100;
            double habitat = _v["rad.habitatIndoorFactor"] * 100;
            double covered = _v["rad.habitatIndoorFactor"] * _v["rad.regolithCoverFactor"] * 100;
            string head = I18n.T(_flareChoice == "shelter" ? "tut.m2.choseShelter" : "tut.m2.choseContinue");
            return head + "\n\n" + I18n.T("tut.m2.compare", outside.ToString("0"), lander.ToString("0"), habitat.ToString("0"), covered.ToString("0"));
        }

        void DrawTargetArrow(Vector3? target)
        {
            if (!target.HasValue) return;
            var t = target.Value;
            var world = new Vector3(t.x, _world.HeightAt(t.x, t.z) + 3f, t.z);
            float dist = new Vector2(t.x - _player.Position.x, t.z - _player.Position.z).magnitude;
            var sp = _player.Cam.WorldToScreenPoint(world);
            bool behind = sp.z < 0;
            var gp = new Vector2(sp.x / _uiScale, (Screen.height - sp.y) / _uiScale);
            if (behind) gp = new Vector2(W - gp.x, H - 40);
            // Off-screen targets get an arrow toward the side to turn to.
            string arrowL = gp.x < 490 ? "← " : "", arrowR = gp.x > W - 140 ? " →" : "";
            gp.x = Mathf.Clamp(gp.x, 490, W - 140);
            gp.y = Mathf.Clamp(gp.y, 80, H - 120);
            var r = new Rect(gp.x - 90, gp.y - 22, 180, 44);
            Fill(r, new Color(0.05f, 0.45f, 0.4f, 0.85f));
            GUI.Label(r, arrowL + I18n.T("tut.marker", dist.ToString("0")) + arrowR, new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 });
        }

        static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
