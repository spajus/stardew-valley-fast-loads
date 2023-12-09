using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace FastLoads {
    internal sealed class ModEntry : Mod {
        public static IMonitor Mon;
        public override void Entry(IModHelper helper) {
            Mon = this.Monitor;
            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();
        }
    }

    public static class StardewFastLoads {
        public static readonly HashSet<int> RoutesFromLocToLocHash = new();
        public static FieldInfo routesField;
        public static MethodInfo exploreMethod;

        [HarmonyPatch(typeof(NPC), "populateRoutesFromLocationToLocationList")]
        public static class NPC_populateRoutesFromLocationToLocationList {
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            public static bool Prefix() {
                ModEntry.Mon.Log($"Clearing RoutesFromLocToLoc before populate: {RoutesFromLocToLocHash.Count}", LogLevel.Info);
                RoutesFromLocToLocHash.Clear();
                return true;
            }
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            public static void Postfix() {
                routesField = GetPrivateField<NPC>("routesFromLocationToLocation");
                var routes = (List<List<string>>)routesField.GetValue(null);

                foreach (var r in routes) {
                    RoutesFromLocToLocHash.Add(HashOf(r));
                }
                ModEntry.Mon.Log($"RoutesFromLocToLoc after populate: {routes.Count}. Hashes: {RoutesFromLocToLocHash.Count}", LogLevel.Info);
            }
        }

        [HarmonyPatch(typeof(NPC), "doesRoutesListContain")]
        public static class NPC_doesRoutesListContain {
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            public static bool Prefix(ref bool __result, List<string> route) {
                // ModEntry.Mon.Log($"RoutesFromLocToLoc during contains check hashes: {routesFromLocToLocHash.Count}", LogLevel.Warn);
                __result = RoutesFromLocToLocHash.Contains(HashOf(route));
                return false;
            }
        }

        [HarmonyPatch(typeof(NPC), "exploreWarpPoints")]
        public static class NPC_exploreWarpPoints {
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            public static bool Prefix(ref bool __result, GameLocation l, List<string> route) {
                // ModEntry.Mon.Log($"Recursive ExploreWarpPoints: {list.Count} / {RoutesFromLocToLocHash.Count}", LogLevel.Warn);
                bool added = false;
                if (l != null && !route.Contains(l.name, StringComparer.Ordinal))
                {
                    route.Add(l.name);
                    if (route.Count == 1 || !RoutesFromLocToLocHash.Contains(HashOf(route)))
                    {
                        if (route.Count > 1)
                        {
                            var r = route.ToList<string>();
                            routesField ??= GetPrivateField<NPC>("routesFromLocationToLocation");
                            var list = (List<List<string>>) routesField.GetValue(null);
                            list.Add(r);
                            RoutesFromLocToLocHash.Add(HashOf(r));
                            added = true;
                        }
                        foreach (Warp warp in l.warps)
                        {
                            string name = warp.TargetName;
                            if (name == "BoatTunnel")
                            {
                                name = "IslandSouth";
                            }
                            if (!name.Equals("Farm", StringComparison.Ordinal) && !name.Equals("Woods", StringComparison.Ordinal) && !name.Equals("Backwoods", StringComparison.Ordinal) && !name.Equals("Tunnel", StringComparison.Ordinal) && !name.Contains("Volcano"))
                            {
                                exploreMethod ??= GetPrivateMethod<NPC>("exploreWarpPoints");
                                exploreMethod.Invoke(null,
                                    new object[] { Game1.getLocationFromName(name), route });
                            }
                        }
                        foreach (Point p in l.doors.Keys)
                        {
                            string name2 = l.doors[p];
                            if (name2 == "BoatTunnel")
                            {
                                name2 = "IslandSouth";
                            }
                            exploreMethod ??= GetPrivateMethod<NPC>("exploreWarpPoints");
                            exploreMethod.Invoke(null,
                                new object[] { Game1.getLocationFromName(name2), route });
                        }
                    }
                    if (route.Count > 0)
                    {
                        route.RemoveAt(route.Count - 1);
                    }
                }
                __result = added;
                return false;
            }
            /*
            // Uncomment to debug, will be very slow
            public static void Postfix()
            {
                var routesField = GetPrivateField<NPC>("routesFromLocationToLocation");
                var routes = (List<List<string>>)routesField.GetValue(null);

                ModEntry.Mon.Log($"RoutesFromLocToLoc: {routes.Count}", LogLevel.Warn);
            }
            */
        }

        private static int HashOf(List<string> strings) {
            var hash = 0;
            for (int i = strings.Count - 1; i >= 0; i--) {
                string s = strings[i];
                hash = hash * 31 + s.GetHashCode();
            }
            return hash;
        }
        private static FieldInfo GetPrivateField<T>(string fieldName) {
            var field = typeof(T).GetField(fieldName, BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static);
            return field;
        }
        private static MethodInfo GetPrivateMethod<T>(string fieldName) {
            var meth = typeof(T).GetMethod(fieldName, BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static);
            return meth;
        }
    }
}
