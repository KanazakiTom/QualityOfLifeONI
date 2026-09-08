//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using HarmonyLib;
//using KMod;
//using UnityEngine;

//namespace QualityOfLifeONI
//{
//    /// <summary>
//    /// Tracks which mod (if any) registered each building prefab, independent of
//    /// whatever descriptor-display API this game build happens to expose.
//    /// Populated once in ModInit.OnAllModsLoaded.
//    /// </summary>
//    public static class ModOriginTracker
//    {
//        // prefabId -> display label already resolved to "Vanilla" / "mod title" / "unknown mod"
//        public static readonly Dictionary<string, string> PrefabIdToOriginLabel = new Dictionary<string, string>();

//        public static void BuildMap(IReadOnlyList<Mod> mods)
//        {
//            PrefabIdToOriginLabel.Clear();

//            try
//            {
//                var baseGameAssembly = typeof(BuildingDef).Assembly;

//                // Build assembly -> mod title lookup, using reflection so this compiles
//                // regardless of exactly which KMod.Mod fields this game version exposes.
//                var assemblyToModTitle = new Dictionary<Assembly, string>();
//                foreach (var mod in mods ?? Array.Empty<Mod>())
//                {
//                    if (mod == null) continue;

//                    string title = ResolveModTitle(mod);
//                    string contentPath = ResolveModContentPath(mod);
//                    if (string.IsNullOrEmpty(title)) continue;

//                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
//                    {
//                        if (asm == baseGameAssembly) continue;
//                        if (assemblyToModTitle.ContainsKey(asm)) continue;

//                        string loc = SafeAssemblyLocation(asm);
//                        if (!string.IsNullOrEmpty(loc) && !string.IsNullOrEmpty(contentPath) &&
//                            loc.IndexOf(contentPath, StringComparison.OrdinalIgnoreCase) >= 0)
//                        {
//                            assemblyToModTitle[asm] = title;
//                        }
//                    }
//                }

//                // Find every IBuildingConfig implementor across all loaded assemblies and
//                // resolve its prefab ID + owning assembly.
//                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
//                {
//                    Type[] types;
//                    try { types = asm.GetTypes(); }
//                    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
//                    catch { continue; }

//                    foreach (var type in types)
//                    {
//                        if (type == null || type.IsAbstract || type.IsInterface) continue;
//                        if (!typeof(IBuildingConfig).IsAssignableFrom(type)) continue;

//                        string prefabId = TryGetStaticOrInstanceId(type);
//                        if (string.IsNullOrEmpty(prefabId)) continue;
//                        if (PrefabIdToOriginLabel.ContainsKey(prefabId)) continue;

//                        string label;
//                        if (asm == baseGameAssembly)
//                        {
//                            label = "Vanilla";
//                        }
//                        else if (assemblyToModTitle.TryGetValue(asm, out var modTitle))
//                        {
//                            label = $"This item is from the mod: \"{modTitle}\"";
//                        }
//                        else
//                        {
//                            label = "This item is from a mod (origin unknown)";
//                        }

//                        PrefabIdToOriginLabel[prefabId] = label;
//                    }
//                }

//                Debug.Log($"[QualityOfLifeONI] WhereThisItemFrom: mapped origin for {PrefabIdToOriginLabel.Count} building prefab(s).");
//            }
//            catch (Exception e)
//            {
//                Debug.LogWarning("[QualityOfLifeONI] WhereThisItemFrom: BuildMap failed, feature will report 'unknown' for everything: " + e);
//            }
//        }

//        public static string GetOriginLabel(string prefabId)
//        {
//            if (!string.IsNullOrEmpty(prefabId) && PrefabIdToOriginLabel.TryGetValue(prefabId, out var label))
//                return label;
//            return "Vanilla";
//        }

//        private static string TryGetStaticOrInstanceId(Type configType)
//        {
//            try
//            {
//                var idField = configType.GetField("ID", BindingFlags.Public | BindingFlags.Static);
//                if (idField != null && idField.FieldType == typeof(string))
//                    return (string)idField.GetValue(null);

//                var idProp = configType.GetProperty("ID", BindingFlags.Public | BindingFlags.Static);
//                if (idProp != null && idProp.PropertyType == typeof(string))
//                    return (string)idProp.GetValue(null);

//                // Fall back to instantiating and checking instance-level ID.
//                if (configType.GetConstructor(Type.EmptyTypes) != null)
//                {
//                    var instance = Activator.CreateInstance(configType);
//                    var instProp = configType.GetProperty("ID", BindingFlags.Public | BindingFlags.Instance);
//                    if (instProp != null && instProp.PropertyType == typeof(string))
//                        return (string)instProp.GetValue(instance);
//                }
//            }
//            catch
//            {
//                // Some config types have constructors with side effects that throw outside
//                // the normal game bootstrap; just skip them rather than crash the whole scan.
//            }
//            return null;
//        }

//        private static string ResolveModTitle(Mod mod)
//        {
//            foreach (var path in new[] { "label.title", "title" })
//            {
//                var val = TryGetNestedMember(mod, path) as string;
//                if (!string.IsNullOrEmpty(val)) return val;
//            }
//            return null;
//        }

//        private static string ResolveModContentPath(Mod mod)
//        {
//            foreach (var path in new[]
//            {
//                "ContentPath", "content_path", "label.install_path",
//                "loaded_content_root", "file_source.GetExtractedFolder"
//            })
//            {
//                var val = TryGetNestedMember(mod, path) as string;
//                if (!string.IsNullOrEmpty(val)) return val;
//            }
//            return null;
//        }

//        // Walks a dotted member path via reflection, trying fields, properties, then
//        // zero-arg methods at each step. Returns null instead of throwing if anything
//        // along the path doesn't exist on this game version.
//        private static object TryGetNestedMember(object obj, string dottedPath)
//        {
//            try
//            {
//                object current = obj;
//                foreach (var part in dottedPath.Split('.'))
//                {
//                    if (current == null) return null;
//                    var t = current.GetType();

//                    var field = t.GetField(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
//                    if (field != null) { current = field.GetValue(current); continue; }

//                    var prop = t.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
//                    if (prop != null) { current = prop.GetValue(current); continue; }

//                    var method = t.GetMethod(part, Type.EmptyTypes);
//                    if (method != null) { current = method.Invoke(current, null); continue; }

//                    return null;
//                }
//                return current;
//            }
//            catch
//            {
//                return null;
//            }
//        }

//        private static string SafeAssemblyLocation(Assembly asm)
//        {
//            try { return asm.Location; }
//            catch { return null; }
//        }
//    }

//    /// <summary>
//    /// Applies (or safely skips) the "Where this item is from" descriptor.
//    /// No [HarmonyPatch] attribute on purpose — this is applied manually from
//    /// ModInit.OnLoad via TryApply(), never through harmony.PatchAll(), so a
//    /// missing/renamed target method on a future game build can never take the
//    /// rest of the mod down with it.
//    /// </summary>
//    public static class WhereThisItemFrom_BuildingDef_Patch
//    {
//        public static void TryApply(Harmony harmony)
//        {
//            try
//            {
//                var targetMethod = TargetMethod();
//                if (targetMethod != null)
//                {
//                    var postfixMethod = AccessTools.Method(typeof(WhereThisItemFrom_BuildingDef_Patch), nameof(Postfix));
//                    harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
//                    Debug.Log("[QualityOfLifeONI] WhereThisItemFrom_BuildingDef_Patch applied to " + targetMethod);
//                }
//                else
//                {
//                    Debug.LogWarning("[QualityOfLifeONI] WhereThisItemFrom: no suitable descriptor method found on BuildingDef " +
//                                      "for this game version. Origin tracking (ModOriginTracker) still works; only the " +
//                                      "in-UI descriptor line is disabled.");
//                }
//            }
//            catch (Exception e)
//            {
//                Debug.LogError("[QualityOfLifeONI] WhereThisItemFrom_BuildingDef_Patch failed to apply, skipping safely: " + e);
//            }
//        }

//        /// <summary>
//        /// Self-discovering target lookup: instead of guessing 2-3 hardcoded signatures,
//        /// scan BuildingDef for anything descriptor-shaped and log every candidate. Even
//        /// on a null result, the Player.log now tells us exactly what BuildingDef exposes
//        /// on this build so the real target can be hardcoded next time.
//        /// </summary>
//        private static MethodBase TargetMethod()
//        {
//            try
//            {
//                var candidates = typeof(BuildingDef)
//                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
//                    .Where(m => m.Name.IndexOf("Descriptor", StringComparison.OrdinalIgnoreCase) >= 0)
//                    .Where(m => m.ReturnType == typeof(List<Descriptor>))
//                    .ToList();

//                if (candidates.Count > 0)
//                {
//                    Debug.Log("[QualityOfLifeONI] WhereThisItemFrom: candidate BuildingDef descriptor method(s): " +
//                              string.Join(" | ", candidates.Select(m => m.ToString())));
//                }
//                else
//                {
//                    Debug.LogWarning("[QualityOfLifeONI] WhereThisItemFrom: BuildingDef exposes no method returning " +
//                                      "List<Descriptor> at all on this game version.");
//                }

//                // Prefer the classic GetDescriptors(GameObject) shape if present.
//                var best = candidates.FirstOrDefault(m =>
//                {
//                    var ps = m.GetParameters();
//                    return ps.Length == 1 && ps[0].ParameterType == typeof(GameObject);
//                });

//                return best ?? candidates.FirstOrDefault();
//            }
//            catch (Exception e)
//            {
//                Debug.LogWarning("[QualityOfLifeONI] WhereThisItemFrom: TargetMethod lookup threw: " + e);
//                return null;
//            }
//        }

//        public static void Postfix(BuildingDef __instance, List<Descriptor> __result)
//        {
//            try
//            {
//                if (__instance == null || __result == null) return;

//                string prefabId = __instance.PrefabID;
//                string label = ModOriginTracker.GetOriginLabel(prefabId);

//                // NOTE: Descriptor's constructor overloads vary a bit by game version.
//                // This is the common (text, tooltip) shape - adjust if your compiler
//                // flags it, e.g. by adding a Descriptor.DescriptorType argument.
//                __result.Add(new Descriptor(label, label));
//            }
//            catch (Exception e)
//            {
//                Debug.LogWarning("[QualityOfLifeONI] Exception in WhereThisItemFrom_BuildingDef_Patch Postfix: " + e.Message);
//            }
//        }
//    }
//}