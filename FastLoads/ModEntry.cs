using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FastLoads {
    internal sealed class ModEntry : Mod {
        public override void Entry(IModHelper helper) {
            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC),
                    "doesRoutesListContain"),
                prefix: new HarmonyMethod(typeof(StardewFastLoads),
                nameof(StardewFastLoads.FastDoesRoutesListContain))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC),
                    "exploreWarpPoints"),
                prefix: new HarmonyMethod(typeof(StardewFastLoads),
                    nameof(StardewFastLoads.FastExploreWarpPoints))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC),
                    "populateRoutesFromLocationToLocationList"),
                postfix: new HarmonyMethod(typeof(StardewFastLoads),
                    nameof(StardewFastLoads.FastPopulateRoutes))
            );
        }
    }

    public static class StardewFastLoads {
        private static readonly HashSet<int> routesFromLocToLocHash = new();

        public static bool FastExploreWarpPoints(GameLocation l, List<string> route) {
            var routesField = GetPrivateField<NPC>("routesFromLocationToLocation");
            var allRoutes = (List<List<string>>) routesField.GetValue(null);
            bool added = false;
            if (l != null && !route.Contains(l.name, StringComparer.Ordinal)) {
                route.Add(l.name);
                if (route.Count == 1 || !routesFromLocToLocHash.Contains(HashOf(route))) {
                    if (route.Count > 1) {
                        var r = route.ToList<string>();
                        allRoutes.Add(r);
                        // main fix in the override:
                        routesFromLocToLocHash.Add(HashOf(r));
                        added = true;
                    }
                    foreach (Warp warp in l.warps) {
                        string name = warp.TargetName;
                        if (name == "BoatTunnel") {
                            name = "IslandSouth";
                        }
                        if (!name.Equals("Farm", StringComparison.Ordinal) &&
                                !name.Equals("Woods", StringComparison.Ordinal) &&
                                !name.Equals("Backwoods", StringComparison.Ordinal) &&
                                !name.Equals("Tunnel", StringComparison.Ordinal) &&
                                !name.Contains("Volcano")) {
                            FastExploreWarpPoints(Game1.getLocationFromName(name), route);
                        }
                    }
                    foreach (Point p in l.doors.Keys) {
                        string name2 = l.doors[p];
                        if (name2 == "BoatTunnel") {
                            name2 = "IslandSouth";
                        }
                        FastExploreWarpPoints(Game1.getLocationFromName(name2), route);
                    }
                }
                if (route.Count > 0)
                {
                    route.RemoveAt(route.Count - 1);
                }
            }
            return added;
        }

        public static void FastPopulateRoutes() {
            var routesField = GetPrivateField<NPC>("routesFromLocationToLocation");

            var routes = (List<List<string>>) routesField.GetValue(null);
            routesFromLocToLocHash.Clear();
            foreach (var r in routes) {
                routesFromLocToLocHash.Add(HashOf(r));
            }
        }

        public static bool FastDoesRoutesListContain(List<string> route) {
            var hash = HashOf(route);
            return routesFromLocToLocHash.Contains(hash);
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
    }
}
