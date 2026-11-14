// AI-assisted code (generated with Claude Code) — see README.
using System;
using System.Collections.Generic;

namespace LunarOutpost.Sim
{
    public enum FacilityKind { Habitat, Solar, Battery, Antenna, LifeSupport, Greenhouse, IceDrill, Shield }

    public enum Resource { Oxygen = 0, Water = 1, Food = 2, Power = 3 }

    public enum PlayerLocation { Outside, Lander, Habitat }

    public enum Phase { Main, Cleared, GameOver }

    /// <summary>World queries. Implemented by the Unity layer; the sim only sees the world through this.</summary>
    public interface IEnv
    {
        bool IsSunlit(float x, float z, double sunAzimuthRad);
        bool IsIceAt(float x, float z);
    }

    /// <summary>All numbers come from data/values.json. A missing key is a bug, so it throws.</summary>
    public sealed class Values
    {
        readonly Dictionary<string, double> _v = new Dictionary<string, double>();
        public readonly Dictionary<string, string> Sources = new Dictionary<string, string>();

        public static Values FromJson(string json)
        {
            var values = new Values();
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            foreach (var kv in root)
            {
                var entry = (Dictionary<string, object>)kv.Value;
                values._v[kv.Key] = (double)entry["value"];
                values.Sources[kv.Key] = (string)entry["source"];
            }
            return values;
        }

        public double this[string key]
        {
            get
            {
                if (!_v.TryGetValue(key, out var v)) throw new KeyNotFoundException($"values.json has no key '{key}'");
                return v;
            }
        }

        public float F(string key) => (float)this[key];
    }

    public sealed class DonkiEvent
    {
        public string Kind;            // "solarFlare" | "sep"
        public string DonkiId;
        public string Date;
        public string ClassOrIntensity;

        public static List<DonkiEvent> ListFromJson(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var list = new List<DonkiEvent>();
            foreach (var o in (List<object>)root["events"])
            {
                var e = (Dictionary<string, object>)o;
                list.Add(new DonkiEvent
                {
                    Kind = (string)e["kind"],
                    DonkiId = (string)e["donkiId"],
                    Date = (string)e["date"],
                    ClassOrIntensity = (string)e["classOrIntensity"],
                });
            }
            return list;
        }
    }

    public sealed class Facility
    {
        public int Id;
        public FacilityKind Kind;
        public float X, Z;
        public bool Sunlit;
        public bool Running;
        public bool RegolithCover;
    }

    public sealed class ActiveEvent
    {
        public DonkiEvent Source;
        public double EndsAtHours;
        public bool Alerted;   // false when comm was down: the warning never arrived
    }

    public sealed class Launch
    {
        public int Supplies, Materials, Crew, Relay;
        public double ArrivesAtHours;
    }

    public sealed class DailyRecord
    {
        public readonly double[] Produced = new double[4];
        public readonly double[] Consumed = new double[4];
        public double MinBatteryRatio = 1;
        public int Downed;
    }

    public sealed class Development
    {
        public double SelfSufficiency, Stability, Safety, Scale, Total, Best;
    }

    public sealed class SimState
    {
        public double TimeHours;
        public double SunAzimuthRad;
        public double OxygenKg, WaterKg, FoodKg, IceKg, Materials;
        public double BatteryKwh;
        public double PowerProducedKw, PowerDemandKw;
        public readonly List<Facility> Facilities = new List<Facility>();
        public int NextFacilityId = 1;
        public int CrewCount;

        public float PlayerX, PlayerZ;
        public PlayerLocation PlayerLocation = PlayerLocation.Lander;
        public double TankHours;
        public double CarriedIceKg;
        public double PlayerDoseMsv, CrewDoseMsv;

        public readonly List<DailyRecord> History = new List<DailyRecord>();
        public DailyRecord Today = new DailyRecord();
        public readonly Development Dev = new Development();

        public ActiveEvent ActiveEvent;
        public readonly List<Launch> Launches = new List<Launch>();
        public double NextLaunchAtHours;
        public int RelaySatellites;
        public bool CommLinked;
        public bool EarthVisible;

        public Phase Phase = Phase.Main;
        public int DaysSurvived;

        public int Count(FacilityKind k)
        {
            int n = 0;
            foreach (var f in Facilities) if (f.Kind == k) n++;
            return n;
        }
    }

    public enum GameEventType
    {
        Built, BuildFailed, FacilityStopped, EventStarted, EventEnded, LaunchOrdered, LaunchFailed, LaunchArrived,
        CommLost, CommRestored, PlayerDowned, DevelopmentChanged, Cleared, GameOver, IceCollected, IceDelivered,
        RegolithCovered,
    }

    public sealed class GameEvent
    {
        public GameEventType Type;
        public string Detail;   // i18n key or id (never raw UI text)
        public GameEvent(GameEventType type, string detail = null) { Type = type; Detail = detail; }
    }

    public abstract class SimCommand { }
    public sealed class BuildCommand : SimCommand { public FacilityKind Kind; public float X, Z; }
    public sealed class CoverWithRegolithCommand : SimCommand { public int FacilityId; }
    public sealed class CollectIceCommand : SimCommand { public float X, Z; }
    public sealed class DeliverIceCommand : SimCommand { }
    public sealed class SetPlayerCommand : SimCommand { public float X, Z; public PlayerLocation Location; }
    public sealed class OrderLaunchCommand : SimCommand { public int Supplies, Materials, Crew, Relay; }
}
