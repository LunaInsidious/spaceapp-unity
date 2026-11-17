using System.Reflection;
using UnityEngine;
var gc = Object.FindAnyObjectByType<LunarOutpost.Game.GameController>();
var bf = BindingFlags.NonPublic | BindingFlags.Instance;
gc.GetType().GetMethod("EnterStep", bf).Invoke(gc, new object[] { 13 });
return "step=" + gc.GetType().GetField("_step", bf).GetValue(gc);
