// AI-assisted code (generated with Claude Code) — see README.
using System;
using System.Collections.Generic;

namespace LunarOutpost.Sim
{
    /// <summary>
    /// The base simulation. No Unity, no UI. Call Step once per fixed game hour.
    /// TODO(人間に確認): SPEC wants an immutable step; this MVP mutates the state in place.
    /// </summary>
    public sealed class Simulation
    {
        public const double StepHours = 1.0;

        readonly Values _v;
        readonly List<DonkiEvent> _donki;
        readonly Func<double> _rng;

        public Simulation(Values values, List<DonkiEvent> donki, Func<double> rng)
        {
            _v = values;
            _donki = donki;
            _rng = rng;
        }

        public SimState NewGame(float landerX, float landerZ)
        {
            var s = new SimState
            {
                OxygenKg = _v["start.oxygenKg"],
                WaterKg = _v["start.waterKg"],
                FoodKg = _v["start.foodKg"],
                Materials = _v["start.materials"],
                CrewCount = (int)_v["crew.startCount"],
                PlayerX = landerX,
                PlayerZ = landerZ,
                PlayerLocation = PlayerLocation.Lander,
                TankHours = _v["player.tankHours"],
            };
            s.NextLaunchAtHours = 0;
            ComputeDevelopment(s);
            return s;
        }

        public static double CostOf(Values v, string item) =>
            Math.Max(v[$"launch.{item}.massKg"], v[$"launch.{item}.volumeM3"] * v["launch.volumetricKgPerM3"]);

        public double LaunchCost(int supplies, int materials, int crew, int relay) =>
            supplies * CostOf(_v, "supplies") + materials * CostOf(_v, "materials") +
            crew * CostOf(_v, "crew") + relay * CostOf(_v, "relay");

        public int CrewCapacity(SimState s) =>
            Math.Min((int)_v["crew.max"], 1 + s.Count(FacilityKind.Habitat) * (int)_v["habitat.crewCapacity"]);

        public double MaterialCost(FacilityKind k) => _v[$"{Key(k)}.materials"];

        public double LifeDays(SimState s)
        {
            double n = s.CrewCount;
            double o = s.OxygenKg / (n * _v["crew.oxygenKgPerDay"]);
            double w = s.WaterKg / (n * _v["crew.waterKgPerDay"]);
            double f = s.FoodKg / (n * _v["crew.foodKgPerDay"]);
            return Math.Max(0, Math.Min(o, Math.Min(w, f)));
        }

        public double BatteryCapacity(SimState s) => s.Count(FacilityKind.Battery) * _v["battery.capacityKwh"];

        public double IndoorFactor(SimState s, PlayerLocation loc)
        {
            if (loc == PlayerLocation.Outside) return 1;
            if (loc == PlayerLocation.Lander) return _v["rad.landerIndoorFactor"];
            double f = _v["rad.habitatIndoorFactor"];
            foreach (var h in s.Facilities)
                if (h.Kind == FacilityKind.Habitat && h.RegolithCover) { f *= _v["rad.regolithCoverFactor"]; break; }
            int walls = Math.Min(s.Count(FacilityKind.Shield), (int)_v["rad.maxShieldWalls"]);
            return f * Math.Pow(_v["rad.shieldWallFactor"], walls);
        }

        /// <summary>Where crew members other than the player shelter.</summary>
        public PlayerLocation CrewShelter(SimState s) =>
            s.Count(FacilityKind.Habitat) > 0 ? PlayerLocation.Habitat : PlayerLocation.Lander;

        public static string Key(FacilityKind k)
        {
            switch (k)
            {
                case FacilityKind.Habitat: return "habitat";
                case FacilityKind.Solar: return "solar";
                case FacilityKind.Battery: return "battery";
                case FacilityKind.Antenna: return "antenna";
                case FacilityKind.LifeSupport: return "lifeSupport";
                case FacilityKind.Greenhouse: return "greenhouse";
                case FacilityKind.IceDrill: return "iceDrill";
                case FacilityKind.Shield: return "shield";
            }
            throw new ArgumentOutOfRangeException(nameof(k));
        }

        /// <summary>Applies a player command right away (UI feedback should not wait for the next tick).</summary>
        public List<GameEvent> ApplyCommand(SimState s, IEnv env, SimCommand c)
        {
            var ev = new List<GameEvent>();
            if (s.Phase != Phase.GameOver) Apply(s, env, c, ev);
            return ev;
        }

        public List<GameEvent> Step(SimState s, IEnv env)
        {
            var ev = new List<GameEvent>();
            if (s.Phase == Phase.GameOver) return ev;

            double dt = StepHours;
            s.TimeHours += dt;
            double period = _v["time.sunPeriodDays"] * 24.0;
            s.SunAzimuthRad = 2 * Math.PI * (s.TimeHours / period % 1.0);

            UpdateComm(s, period, ev);
            UpdatePowerAndProduction(s, env, dt, ev);
            ConsumeByCrew(s, dt);
            UpdatePlayer(s, dt, ev);
            UpdateSpaceWeather(s, ev);
            UpdateLaunches(s, ev);

            if (s.OxygenKg <= 0) { GameOver(s, "oxygen", ev); return ev; }
            if (s.WaterKg <= 0) { GameOver(s, "water", ev); return ev; }
            if (s.FoodKg <= 0) { GameOver(s, "food", ev); return ev; }

            if ((long)Math.Round(s.TimeHours) % 24 == 0) EndOfDay(s, ev);
            return ev;
        }

        void GameOver(SimState s, string reason, List<GameEvent> ev)
        {
            s.OxygenKg = Math.Max(0, s.OxygenKg);
            s.WaterKg = Math.Max(0, s.WaterKg);
            s.FoodKg = Math.Max(0, s.FoodKg);
            s.Phase = Phase.GameOver;
            ev.Add(new GameEvent(GameEventType.GameOver, reason));
        }

        void Apply(SimState s, IEnv env, SimCommand c, List<GameEvent> ev)
        {
            switch (c)
            {
                case BuildCommand b:
                {
                    double cost = MaterialCost(b.Kind);
                    if (s.Materials < cost) { ev.Add(new GameEvent(GameEventType.BuildFailed, "msg.noMaterials")); return; }
                    if (b.Kind == FacilityKind.IceDrill && !env.IsIceAt(b.X, b.Z)) { ev.Add(new GameEvent(GameEventType.BuildFailed, "msg.drillNeedsIce")); return; }
                    s.Materials -= cost;
                    var f = new Facility { Id = s.NextFacilityId++, Kind = b.Kind, X = b.X, Z = b.Z };
                    f.Sunlit = env.IsSunlit(f.X, f.Z, s.SunAzimuthRad);
                    s.Facilities.Add(f);
                    ev.Add(new GameEvent(GameEventType.Built, f.Id.ToString()));
                    break;
                }
                case CoverWithRegolithCommand r:
                {
                    var h = s.Facilities.Find(f => f.Id == r.FacilityId);
                    if (h == null || h.Kind != FacilityKind.Habitat || h.RegolithCover) return;
                    if (s.Materials < _v["rad.regolithCoverMaterials"]) { ev.Add(new GameEvent(GameEventType.BuildFailed, "msg.noMaterials")); return; }
                    s.Materials -= _v["rad.regolithCoverMaterials"];
                    s.PlayerDoseMsv += _v["rad.regolithCoverDoseMsv"];
                    h.RegolithCover = true;
                    ev.Add(new GameEvent(GameEventType.RegolithCovered));
                    break;
                }
                case CollectIceCommand ice:
                    if (!env.IsIceAt(ice.X, ice.Z)) return;
                    if (s.CarriedIceKg >= _v["player.iceCarryKg"]) return;
                    s.CarriedIceKg = _v["player.iceCarryKg"];
                    ev.Add(new GameEvent(GameEventType.IceCollected));
                    break;
                case DeliverIceCommand _:
                    if (s.CarriedIceKg <= 0) return;
                    s.IceKg += s.CarriedIceKg;
                    s.CarriedIceKg = 0;
                    ev.Add(new GameEvent(GameEventType.IceDelivered));
                    break;
                case SetPlayerCommand p:
                    s.PlayerX = p.X;
                    s.PlayerZ = p.Z;
                    s.PlayerLocation = p.Location;
                    break;
                case OrderLaunchCommand o:
                    OrderLaunch(s, o, ev);
                    break;
                default:
                    throw new ArgumentException($"Unknown command {c.GetType().Name}");
            }
        }

        public string CanOrderLaunch(SimState s, int supplies, int materials, int crew, int relay)
        {
            if (!s.CommLinked) return "msg.launchNoComm";
            if (s.TimeHours < s.NextLaunchAtHours) return "msg.launchNotReady";
            if (supplies + materials + crew + relay == 0) return "msg.launchEmpty";
            if (LaunchCost(supplies, materials, crew, relay) > _v["launch.capKg"]) return "msg.launchOverCap";
            int incomingCrew = 0;
            foreach (var l in s.Launches) incomingCrew += l.Crew;
            if (s.CrewCount + incomingCrew + crew > CrewCapacity(s)) return "msg.launchNoBeds";
            if (s.RelaySatellites + relay > 2) return "msg.launchRelayMax";
            return null;
        }

        void OrderLaunch(SimState s, OrderLaunchCommand o, List<GameEvent> ev)
        {
            string err = CanOrderLaunch(s, o.Supplies, o.Materials, o.Crew, o.Relay);
            if (err != null) { ev.Add(new GameEvent(GameEventType.LaunchFailed, err)); return; }
            s.Launches.Add(new Launch
            {
                Supplies = o.Supplies, Materials = o.Materials, Crew = o.Crew, Relay = o.Relay,
                ArrivesAtHours = s.TimeHours + _v["launch.transitDays"] * 24,
            });
            s.NextLaunchAtHours = s.TimeHours + _v["launch.intervalDays"] * 24;
            ev.Add(new GameEvent(GameEventType.LaunchOrdered));
        }

        void UpdateComm(SimState s, double period, List<GameEvent> ev)
        {
            // Earth sets for part of each month at the south pole (simplified; no orbit computed — SPEC/LCRNS).
            double phase = Math.Cos(2 * Math.PI * s.TimeHours / period);
            s.EarthVisible = phase > _v["comm.earthSetThreshold"];
            bool reachable = s.RelaySatellites >= 2 || s.EarthVisible ||
                             (s.RelaySatellites == 1 && phase > _v["comm.oneRelayThreshold"]);
            bool hasAntenna = s.Facilities.Exists(f => f.Kind == FacilityKind.Antenna && f.Running);
            // The antenna needs power, so check the previous hour's running state.
            bool linked = hasAntenna && reachable;
            if (linked != s.CommLinked) ev.Add(new GameEvent(linked ? GameEventType.CommRestored : GameEventType.CommLost));
            s.CommLinked = linked;
        }

        static readonly FacilityKind[] PowerPriority =
        {
            FacilityKind.Habitat, FacilityKind.LifeSupport, FacilityKind.Antenna, FacilityKind.Greenhouse, FacilityKind.IceDrill,
        };

        void UpdatePowerAndProduction(SimState s, IEnv env, double dt, List<GameEvent> ev)
        {
            double produced = 0;
            foreach (var f in s.Facilities)
            {
                if (f.Kind != FacilityKind.Solar) continue;
                f.Sunlit = env.IsSunlit(f.X, f.Z, s.SunAzimuthRad);
                bool was = f.Running;
                f.Running = f.Sunlit;
                if (was && !f.Running) ev.Add(new GameEvent(GameEventType.FacilityStopped, "msg.solarShadow"));
                if (f.Running) produced += _v["solar.outputKw"] * dt;
            }

            double available = produced + s.BatteryKwh;
            double demand = 0;
            foreach (var kind in PowerPriority)
            {
                foreach (var f in s.Facilities)
                {
                    if (f.Kind != kind) continue;
                    double need = _v[$"{Key(kind)}.powerKw"] * dt;
                    demand += need;
                    bool was = f.Running;
                    f.Running = available >= need;
                    if (f.Running) available -= need;
                    else if (was) ev.Add(new GameEvent(GameEventType.FacilityStopped, "msg.noPower"));
                }
            }
            foreach (var f in s.Facilities)
                if (f.Kind == FacilityKind.Battery || f.Kind == FacilityKind.Shield) f.Running = true;

            double used = produced + s.BatteryKwh - available;
            double cap = BatteryCapacity(s);
            s.BatteryKwh = Math.Max(0, Math.Min(cap, s.BatteryKwh + produced - used));
            s.PowerProducedKw = produced / dt;
            s.PowerDemandKw = demand / dt;
            s.Today.Produced[(int)Resource.Power] += Math.Min(produced, demand);
            s.Today.Consumed[(int)Resource.Power] += demand;
            s.Today.MinBatteryRatio = Math.Min(s.Today.MinBatteryRatio, cap > 0 ? s.BatteryKwh / cap : 0);

            foreach (var f in s.Facilities)
            {
                if (!f.Running) continue;
                switch (f.Kind)
                {
                    case FacilityKind.IceDrill:
                        s.IceKg += _v["iceDrill.iceKgPerHour"] * dt;
                        break;
                    case FacilityKind.LifeSupport:
                    {
                        double melt = Math.Min(s.IceKg, _v["lifeSupport.iceKgPerHour"] * dt);
                        s.IceKg -= melt;
                        s.WaterKg += melt;
                        s.Today.Produced[(int)Resource.Water] += melt;
                        double split = Math.Min(s.WaterKg, _v["lifeSupport.electrolysisWaterKgPerHour"] * dt);
                        s.WaterKg -= split;
                        s.Today.Consumed[(int)Resource.Water] += split;
                        double o2 = split * _v["lifeSupport.oxygenPerWaterKg"];
                        s.OxygenKg += o2;
                        s.Today.Produced[(int)Resource.Oxygen] += o2;
                        break;
                    }
                    case FacilityKind.Greenhouse:
                    {
                        double w = _v["greenhouse.waterKgPerHour"] * dt;
                        if (s.WaterKg < w) break;
                        s.WaterKg -= w;
                        s.Today.Consumed[(int)Resource.Water] += w;
                        double food = _v["greenhouse.foodKgPerHour"] * dt;
                        s.FoodKg += food;
                        s.Today.Produced[(int)Resource.Food] += food;
                        break;
                    }
                }
            }
        }

        void ConsumeByCrew(SimState s, double dt)
        {
            double n = s.CrewCount;
            double o = n * _v["crew.oxygenKgPerDay"] / 24 * dt;
            double w = n * _v["crew.waterKgPerDay"] / 24 * dt;
            double f = n * _v["crew.foodKgPerDay"] / 24 * dt;
            s.OxygenKg -= o; s.WaterKg -= w; s.FoodKg -= f;
            s.Today.Consumed[(int)Resource.Oxygen] += o;
            s.Today.Consumed[(int)Resource.Water] += w;
            s.Today.Consumed[(int)Resource.Food] += f;
        }

        void UpdatePlayer(SimState s, double dt, List<GameEvent> ev)
        {
            double tankMax = _v["player.tankHours"];
            if (s.PlayerLocation == PlayerLocation.Outside)
            {
                s.TankHours -= dt;
                if (s.TankHours <= 0)
                {
                    s.TankHours = tankMax;
                    s.CarriedIceKg = 0;
                    s.PlayerLocation = PlayerLocation.Lander;
                    s.Today.Downed++;
                    ev.Add(new GameEvent(GameEventType.PlayerDowned, "msg.downedTank"));
                }
            }
            else
            {
                s.TankHours = tankMax;
            }

            double flare = s.ActiveEvent != null ? _v["rad.flareOutdoorMsvPerDay"] : 0;
            double rate = (_v["rad.surfaceMsvPerDay"] + flare) / 24 * dt;
            s.PlayerDoseMsv += rate * IndoorFactor(s, s.PlayerLocation);
            s.CrewDoseMsv += rate * IndoorFactor(s, CrewShelter(s));
        }

        void UpdateSpaceWeather(SimState s, List<GameEvent> ev)
        {
            if (s.ActiveEvent != null)
            {
                if (s.TimeHours >= s.ActiveEvent.EndsAtHours)
                {
                    s.ActiveEvent = null;
                    ev.Add(new GameEvent(GameEventType.EventEnded));
                }
                return;
            }
            // TODO(人間に確認): SPEC replays a real DONKI period day by day. MVP draws archived events at random.
            if (_rng() < _v["event.flareChancePerDay"] / 24.0)
            {
                var src = _donki[(int)(_rng() * _donki.Count) % _donki.Count];
                s.ActiveEvent = new ActiveEvent
                {
                    Source = src,
                    EndsAtHours = s.TimeHours + _v["event.flareDurationHours"],
                    Alerted = s.CommLinked,
                };
                ev.Add(new GameEvent(GameEventType.EventStarted, src.DonkiId));
            }
        }

        void UpdateLaunches(SimState s, List<GameEvent> ev)
        {
            for (int i = s.Launches.Count - 1; i >= 0; i--)
            {
                var l = s.Launches[i];
                if (s.TimeHours < l.ArrivesAtHours) continue;
                s.OxygenKg += l.Supplies * _v["launch.supplies.oxygenKg"];
                s.WaterKg += l.Supplies * _v["launch.supplies.waterKg"];
                s.FoodKg += l.Supplies * _v["launch.supplies.foodKg"];
                s.Materials += l.Materials * _v["launch.materials.units"];
                s.CrewCount += l.Crew;
                s.RelaySatellites += l.Relay;
                s.Launches.RemoveAt(i);
                ev.Add(new GameEvent(GameEventType.LaunchArrived));
            }
        }

        void EndOfDay(SimState s, List<GameEvent> ev)
        {
            s.DaysSurvived++;
            s.History.Add(s.Today);
            int keep = (int)Math.Max(_v["dev.selfWindowDays"], _v["dev.batteryWindowDays"]);
            while (s.History.Count > keep) s.History.RemoveAt(0);
            s.Today = new DailyRecord();

            double before = s.Dev.Total;
            ComputeDevelopment(s);
            if (Math.Abs(before - s.Dev.Total) > 0.01) ev.Add(new GameEvent(GameEventType.DevelopmentChanged));
            if (s.Phase == Phase.Main && s.Dev.Total >= _v["dev.clearThreshold"])
            {
                s.Phase = Phase.Cleared;
                ev.Add(new GameEvent(GameEventType.Cleared));
            }
        }

        public void ComputeDevelopment(SimState s)
        {
            double min = _v["dev.minElement"];

            // Self-sufficiency: the weakest local-supply ratio of O2, water, food, power over the window.
            double self = 0;
            if (s.History.Count > 0)
            {
                self = 1;
                int window = (int)_v["dev.selfWindowDays"];
                for (int r = 0; r < 4; r++)
                {
                    double p = 0, c = 0;
                    for (int i = Math.Max(0, s.History.Count - window); i < s.History.Count; i++)
                    {
                        p += s.History[i].Produced[r];
                        c += s.History[i].Consumed[r];
                    }
                    double ratio = c > 0 ? Math.Min(1, p / c) : (r == (int)Resource.Power ? 1 : 0);
                    self = Math.Min(self, ratio);
                }
            }

            // Stability: battery low point, life-support days, equipment condition (no wear in MVP: 1).
            double battery = 0;
            int bw = (int)_v["dev.batteryWindowDays"];
            if (s.History.Count > 0 && BatteryCapacity(s) > 0)
            {
                battery = 1;
                for (int i = Math.Max(0, s.History.Count - bw); i < s.History.Count; i++)
                    battery = Math.Min(battery, s.History[i].MinBatteryRatio);
            }
            double life = Math.Min(1, LifeDays(s) / _v["dev.lifeDaysForFull"]);
            double condition = 1; // TODO(人間に確認): equipment wear is out of MVP scope
            double stability = (battery + life + condition) / 3;

            // Safety: distance from the dose limit, penalised by recent downs.
            double avgDose = (s.PlayerDoseMsv + s.CrewDoseMsv * (s.CrewCount - 1)) / s.CrewCount;
            int downs = s.Today.Downed;
            foreach (var h in s.History) downs += h.Downed;
            double safety = Math.Max(0, 1 - avgDose / _v["rad.doseLimitMsv"]) * Math.Pow(_v["dev.downPenalty"], downs);

            double scale = _v["dev.scalePerCrew"] * s.CrewCount; // + upgrades (out of MVP scope)

            s.Dev.SelfSufficiency = Math.Max(min, self * 100);
            s.Dev.Stability = Math.Max(min, stability * 100);
            s.Dev.Safety = Math.Max(min, safety * 100);
            s.Dev.Scale = Math.Max(min, scale);
            s.Dev.Total = Math.Pow(s.Dev.SelfSufficiency * s.Dev.Stability * s.Dev.Safety * s.Dev.Scale, 0.25);
            s.Dev.Best = Math.Max(s.Dev.Best, s.Dev.Total);
        }
    }
}
