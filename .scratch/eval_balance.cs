using System.Reflection;
using System.Text;
using UnityEngine;
using LunarOutpost.Sim;
var gc = Object.FindAnyObjectByType<LunarOutpost.Game.GameController>();
var bf = BindingFlags.NonPublic | BindingFlags.Instance;
var world = (LunarOutpost.Game.MoonWorld)gc.GetType().GetField("_world", bf).GetValue(gc);
var v = Values.FromJson(Resources.Load<TextAsset>("data/values").text);
var donki = DonkiEvent.ListFromJson(Resources.Load<TextAsset>("data/donki-events").text);
var rnd = new System.Random(1);
var sim = new Simulation(v, donki, () => rnd.NextDouble());
var s = sim.NewGame(0, 0);
s.PlayerLocation = PlayerLocation.Habitat;
void B(FacilityKind k, float x, float z) { var e = sim.ApplyCommand(s, world, new BuildCommand { Kind = k, X = x, Z = z }); }
B(FacilityKind.Habitat, -12, 8); B(FacilityKind.Solar, 10, -10); B(FacilityKind.Solar, 16, -6);
B(FacilityKind.Battery, 8, 6); B(FacilityKind.Antenna, -6, -14); B(FacilityKind.LifeSupport, -18, -4);
var sb = new StringBuilder();
for (int h = 1; h <= 24 * 40; h++)
{
    foreach (var e in sim.Step(s, world)) if (e.Type == GameEventType.GameOver || e.Type == GameEventType.Cleared) sb.Append($"[{e.Type} {e.Detail} d{h/24}]");
    if (h == 24) { sim.ApplyCommand(s, world, new OrderLaunchCommand { Materials = 2, Supplies = 1 }); }
    if (h == 24 * 4) { B(FacilityKind.IceDrill, 75, 70); B(FacilityKind.Greenhouse, -20, 14); B(FacilityKind.Battery, 12, 10); B(FacilityKind.Solar, 20, -2); }
    if (h == 24 * 11) { sim.ApplyCommand(s, world, new OrderLaunchCommand { Crew = 1, Supplies = 2 }); }
    if (h == 24 * 15) { B(FacilityKind.Greenhouse, -24, 20); B(FacilityKind.Solar, 24, 4); B(FacilityKind.Battery, 14, 14); }
    if (h % 120 == 0) sb.Append($"d{h/24}: dev={s.Dev.Total:0} self={s.Dev.SelfSufficiency:0} stab={s.Dev.Stability:0} safe={s.Dev.Safety:0} scale={s.Dev.Scale:0} O2={s.OxygenKg:0} W={s.WaterKg:0} F={s.FoodKg:0} ice={s.IceKg:0} bat={s.BatteryKwh:0} mat={s.Materials:0} crew={s.CrewCount} comm={s.CommLinked}\n");
}
return sb.ToString();
