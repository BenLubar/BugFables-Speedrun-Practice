using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SpeedrunPractice.Extensions;
using UnityEngine;

namespace SpeedrunPractice.Patches
{
    [HarmonyPatch(typeof(MazeGame), "Start")]
    public class PatchMazeGameStart
    {
        static bool Prefix(MazeGame __instance)
        {
            AccessTools.FieldRefAccess<MazeGame, Sprite[]>(__instance, "sprites") = Resources.LoadAll<Sprite>("Sprites/Misc/dungeongame");
            AccessTools.FieldRefAccess<MazeGame, Transform>(__instance, "holder") = __instance.transform;

            var rng = __instance.gameObject.AddComponent<RNG512>();
            rng.SetSeed(RNG512.RandomSeed());

            // generate all floors and remember the RNG state after each
            rng.SaveState(0);
            ref int floor = ref AccessTools.FieldRefAccess<MazeGame, int>(__instance, "floor");
            ref int maxFloors = ref AccessTools.FieldRefAccess<MazeGame, int>(__instance, "maxfloors");
            ref int[] mapSize = ref AccessTools.FieldRefAccess<MazeGame, int[]>(__instance, "mapsize");
            for (floor = 0; floor < maxFloors; floor++)
            {
                int sizeX = mapSize[0] + 5 * floor;
                int sizeY = mapSize[1] + 5 * floor;
                if (floor == 0)
                {
                    floor = -1;
                }
                var generating = (IEnumerator)AccessTools.Method(typeof(MazeGame), "Generate").Invoke(__instance, new object[] { sizeX, sizeY, true });
                while (generating.MoveNext())
                {
                }
                if (floor == -1)
                {
                    floor = 0;
                }
                rng.SaveState(floor + 1);
                __instance.caninput = false;
            }

            // remember where we were but reset RNG to initial state
            rng.SaveState(-1);
            rng.LoadState(0);
            floor = 0;

            // clear out the mess we made
            for (int i = 0; i < __instance.transform.childCount; i++)
            {
                UnityEngine.Object.Destroy(__instance.transform.GetChild(i).gameObject);
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(MazeGame), "NewEnemy")]
    public class PatchMazeGameNewEnemy
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(
                AccessTools.Method(typeof(MazeGame), "GetRandomPointInRoom"),
                AccessTools.Method(typeof(PatchMazeGameNewEnemy), nameof(FencepostRandomPointInRoom))
            );
        }

        public static int[] FencepostRandomPointInRoom(MazeGame instance, int roomID)
        {
            var bounds = AccessTools.FieldRefAccess<MazeGame, List<int[]>>(instance, "roomdata")[roomID];
            return new int[]
            {
                (int)instance.GetComponent<RNG512>().Int(bounds[0], bounds[2] + 1),
                (int)instance.GetComponent<RNG512>().Int(bounds[1], bounds[3] + 1),
            };
        }
    }

    [HarmonyPatch]
    public class PatchMazeGameGenerate
    {
        private static readonly MethodInfo getComponentRNG = AccessTools.Method(typeof(MonoBehaviour), nameof(MonoBehaviour.GetComponent), new Type[0], new[] { typeof(RNG512) });
        private static readonly FieldInfo canInput = AccessTools.Field(typeof(MazeGame), "caninput");
        private static readonly MethodInfo rngLoadStateIfExists = AccessTools.Method(typeof(RNG512), nameof(RNG512.LoadStateIfExists), new[] { typeof(int) });

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(MazeGame).GetNestedTypes(AccessTools.all).First((t) => t.Name.StartsWith("<Generate>c__Iterator")), "MoveNext");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var func = new List<CodeInstruction>(instructions);
            for (int i = 0; i < func.Count; i++)
            {
                if (func[i].StoresField(canInput))
                {
                    func.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(original.DeclaringType, "$this")),
                        new CodeInstruction(OpCodes.Call, getComponentRNG),
                        new CodeInstruction(OpCodes.Ldc_I4_M1),
                        new CodeInstruction(OpCodes.Call, rngLoadStateIfExists),
                    });
                }
            }
            return func;
        }
    }

    [HarmonyPatch]
    public class PatchMazeGameFloorChange
    {
        private static readonly MethodInfo getComponentRNG = AccessTools.Method(typeof(MonoBehaviour), nameof(MonoBehaviour.GetComponent), new Type[0], new[] { typeof(RNG512) });
        private static readonly MethodInfo generate = AccessTools.Method(typeof(MazeGame), "Generate", new[] { typeof(int), typeof(int), typeof(bool) });
        private static readonly MethodInfo rngSaveState = AccessTools.Method(typeof(RNG512), nameof(RNG512.SaveState), new[] { typeof(int) });
        private static readonly MethodInfo rngLoadState = AccessTools.Method(typeof(RNG512), nameof(RNG512.LoadState), new[] { typeof(int) });
        private static readonly FieldInfo floor = AccessTools.Field(typeof(MazeGame), "floor");

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(MazeGame).GetNestedTypes(AccessTools.all).First((t) => t.Name.StartsWith("<FloorChange>c__Iterator")), "MoveNext");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var func = new List<CodeInstruction>(instructions);
            for (int i = 0; i < func.Count; i++)
            {
                if (func[i].Calls(generate))
                {
                    func.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(original.DeclaringType, "$this")),
                        new CodeInstruction(OpCodes.Call, getComponentRNG),
                        new CodeInstruction(OpCodes.Ldc_I4_M1),
                        new CodeInstruction(OpCodes.Call, rngSaveState),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(original.DeclaringType, "$this")),
                        new CodeInstruction(OpCodes.Call, getComponentRNG),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(original.DeclaringType, "$this")),
                        new CodeInstruction(OpCodes.Ldfld, floor),
                        new CodeInstruction(OpCodes.Call, rngLoadState),
                    });
                    break;
                }
            }
            return func;
        }
    }

    [HarmonyPatch]
    public class PatchMazeGameEndGame
    {
        private static readonly FieldInfo score = AccessTools.Field(typeof(MazeGame), "score");
        private static readonly MethodInfo callSetText = AccessTools.Method(typeof(PatchMazeGameEndGame), nameof(SetSeedText), new[] { typeof(MazeGame) });

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(MazeGame).GetNestedTypes(AccessTools.all).First((t) => t.Name.StartsWith("<EndGame>c__Iterator")), "MoveNext");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var func = new List<CodeInstruction>(instructions);
            for (int i = 0; i < func.Count; i++)
            {
                if (func[i].StoresField(score))
                {
                    func.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(original.DeclaringType, "$this")),
                        new CodeInstruction(OpCodes.Call, callSetText),
                    });
                }
            }
            return func;
        }
        private static void SetSeedText(MazeGame mk)
        {
            // (this uses 0.5 gray rather than 0.8 gray, but fixing that would require much more code)
            mk.StartCoroutine(MainManager.SetText(FlappyBee.args + "|center||color,5|Seed: " + mk.GetComponent<RNG512>().Seed, 1, null, false, true, new Vector3(-10f, 16f, -0.1f), Vector3.one, Vector3.one, mk.transform, null));
        }
    }

    [HarmonyPatch(typeof(MazeGame))]
    [HarmonyPatchAll]
    public class PatchMazeGameRNG
    {
        private static readonly MethodInfo getComponentRNG = AccessTools.Method(typeof(MonoBehaviour), nameof(MonoBehaviour.GetComponent), new Type[0], new[] { typeof(RNG512) });
        private static readonly MethodInfo rangeFloat = AccessTools.Method(typeof(UnityEngine.Random), "Range", new[] { typeof(float), typeof(float) });
        private static readonly MethodInfo rangeInt = AccessTools.Method(typeof(UnityEngine.Random), "Range", new[] { typeof(int), typeof(int) });
        private static readonly MethodInfo replacementRangeFloat = AccessTools.Method(typeof(RNG512), "RangeFloat");
        private static readonly MethodInfo replacementRangeInt = AccessTools.Method(typeof(RNG512), "RangeInt");

        internal static IEnumerable<CodeInstruction> TranspileWithThis(IEnumerable<CodeInstruction> instructions, IEnumerable<CodeInstruction> getThis)
        {
            var func = new List<CodeInstruction>(instructions);

            for (var i = 2; i < func.Count; i++)
            {
                if (func[i].Calls(rangeFloat) || func[i].Calls(rangeInt))
                {
                    func[i] = new CodeInstruction(OpCodes.Call, func[i].Calls(rangeFloat) ? replacementRangeFloat : replacementRangeInt);
                    func.InsertRange(i, getThis.Concat(new[]
                    {
                        new CodeInstruction(OpCodes.Call, getComponentRNG),
                    }));
                    i += 1 + getThis.Count();
                }
            }

            return func;
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return TranspileWithThis(instructions, new[] { new CodeInstruction(OpCodes.Ldarg_0) });
        }
    }

    [HarmonyPatch]
    public class PatchMazeGameIteratorRNG
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(MazeGame).GetNestedTypes(AccessTools.all).Where((t) => !t.IsValueType).Select<Type, MethodBase>((t) => AccessTools.Method(t, "MoveNext"));
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            return PatchMazeGameRNG.TranspileWithThis(instructions, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(original.DeclaringType, "$this")),
            });
        }
    }
}
