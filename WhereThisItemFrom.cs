using HarmonyLib;
using KMod;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace QualityOfLifeONI
{ 
    public static class WhereThisItemFromHelper
    {
        public static readonly Dictionary<string, string> PrefabToModMap = new Dictionary<string, string>();
        private static readonly Dictionary<Assembly, string> AssemblyToModNameCache = new Dictionary<Assembly, string>();
        private static readonly HashSet<Assembly> VanillaAssemblies = new HashSet<Assembly>();

        /// <summary>
        /// Maps a C# Assembly to its corresponding Mod title from mod.yaml
        /// </summary>
        public static string GetModNameFromAssembly(Assembly asm)
        {
            if (asm == null) return null;

            if (AssemblyToModNameCache.TryGetValue(asm, out string cachedName))
                return cachedName;

            if (VanillaAssemblies.Contains(asm))
                return null;

            // Exclude game and engine assemblies
            string asmName = asm.GetName().Name;
            if (asmName.StartsWith("System") ||
                asmName.StartsWith("UnityEngine") ||
                asmName.StartsWith("Assembly-CSharp") ||
                asmName.StartsWith("mscorlib") ||
                asmName.StartsWith("HarmonyLib") ||
                asmName.StartsWith("0Harmony") ||
                asmName.StartsWith("KNet"))
            {
                VanillaAssemblies.Add(asm);
                return null;
            }

            // Look up in Klei ModManager loaded mods
            if (Global.Instance != null && Global.Instance.modManager != null)
            {
                foreach (Mod mod in Global.Instance.modManager.mods)
                {
                    if (mod == null || !mod.enabled) continue;

                    if (mod.loaded_mod_data != null && mod.loaded_mod_data.dlls != null)
                    {
                        foreach (Assembly modAsm in mod.loaded_mod_data.dlls)
                        {
                            if (modAsm == asm)
                            {
                                string title = !string.IsNullOrEmpty(mod.title) ? mod.title : mod.label.title;
                                AssemblyToModNameCache[asm] = title;
                                return title;
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves origin mod name for any given GameObject
        /// </summary>
        public static string GetModNameForItem(GameObject go)
        {
            if (go == null) return null;

            // 1. Check Prefab ID cache
            KPrefabID kpid = go.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                string id = kpid.PrefabID().Name;
                if (!string.IsNullOrEmpty(id) && PrefabToModMap.TryGetValue(id, out string modName))
                {
                    return modName;
                }
            }

            // 2. Check BuildingDef cache
            BuildingDef def = null;
            BuildingComplete bc = go.GetComponent<BuildingComplete>();
            if (bc != null) def = bc.Def
            if (def == null)
            {
                BuildingUnderConstruction buc = go.GetComponent<BuildingUnderConstruction>();
                if (buc != null) def = buc.Def;
            }
            if (def == null)
            {
                BuildingPreview bp = go.GetComponent<BuildingPreview>();
                if (bp != null) def = bp.Def;
            }

            if (def != null && !string.IsNullOrEmpty(def.PrefabID))
            {
                if (PrefabToModMap.TryGetValue(def.PrefabID, out string modName))
                {
                    return modName;
                }
            }

            // 3. Fallback: inspect scripts attached to the GameObject
            MonoBehaviour[] components = go.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour comp in components)
            {
                if (comp == null) continue;
                string modName = GetModNameFromAssembly(comp.GetType().Assembly);
                if (!string.IsNullOrEmpty(modName))
                {
                    return modName;
                }
            }

            return null;
        }
    }

    // --- Harmony Patches ---

    // 1. Capture Building registrations
    [HarmonyPatch(typeof(BuildingConfigManager), nameof(BuildingConfigManager.RegisterBuilding))]
    public static class BuildingConfigManager_RegisterBuilding_Patch
    {
        public static void Postfix(IBuildingConfig config, BuildingDef __result)
        {
            if (config == null || __result == null) return;
            Assembly asm = config.GetType().Assembly;
            string modName = WhereThisItemFromHelper.GetModNameFromAssembly(asm);
            if (!string.IsNullOrEmpty(modName))
            {
                WhereThisItemFromHelper.PrefabToModMap[__result.PrefabID] = modName;
            }
        }
    }

    // 2. Capture Entity/Item registrations
    [HarmonyPatch(typeof(Assets), nameof(Assets.AddPrefab))]
    public static class Assets_AddPrefab_Patch
    {
        public static void Postfix(KPrefabID prefab)
        {
            if (prefab == null) return;

            StackTrace st = new StackTrace();
            for (int i = 0; i < st.FrameCount; i++)
            {
                MethodBase method = st.GetFrame(i)?.GetMethod();
                if (method == null || method.DeclaringType == null) continue;

                Assembly asm = method.DeclaringType.Assembly;
                string modName = WhereThisItemFromHelper.GetModNameFromAssembly(asm);
                if (!string.IsNullOrEmpty(modName))
                {
                    WhereThisItemFromHelper.PrefabToModMap[prefab.PrefabID().Name] = modName;
                    break;
                }
            }
        }
    }

    // 3. Append origin info descriptor to the bottom of the INFORMATION tab
    [HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetAllDescriptors))]
    public static class GameUtil_GetAllDescriptors_Patch
    {
        public static void Postfix(GameObject go, ref List<Descriptor> __result)
        {
            if (go == null || __result == null) return;

            string modName = WhereThisItemFromHelper.GetModNameForItem(go);

            string formattedText = string.IsNullOrEmpty(modName)
                ? "<i><color=#318CE7>Vanilla</color></i>"
                : $"<i><color=#318CE7>This item from the mod \"{modName}\"</color></i>";

            __result.Add(new Descriptor(
                formattedText,
                formattedText,
                Descriptor.DescriptorType.Information,
                false
            ));
        }
    }
}