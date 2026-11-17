using System.Reflection;
using UnityEngine;
var gc = Object.FindAnyObjectByType<LunarOutpost.Game.GameController>();
var bf = BindingFlags.NonPublic | BindingFlags.Instance;
var t = gc.GetType();
t.GetMethod("EnterShelter", bf).Invoke(gc, new object[] { LunarOutpost.Sim.PlayerLocation.Lander, 0 });
t.GetMethod("OnFlareChoice", bf).Invoke(gc, new object[] { "shelter" });
return "ok";
