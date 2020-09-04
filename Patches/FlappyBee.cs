using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SpeedrunPractice.Extensions;
using UnityEngine;

namespace SpeedrunPractice.Patches
{
    [HarmonyPatch(typeof(FlappyBee), "Start")]
    public class PatchFlappyBeeStart
    {
        static bool Prefix(FlappyBee __instance)
        {
            __instance.gameObject.AddComponent<RNG512>().SetSeed(RNG512.RandomSeed());
            return true;
        }
    }

    [HarmonyPatch(typeof(FlappyBee))]
    [HarmonyPatchAll]
    public class PatchFlappyBeeRNG
    {
        private static readonly MethodInfo getComponentRNG = AccessTools.Method(typeof(MonoBehaviour), nameof(MonoBehaviour.GetComponent), new Type[0], new[] { typeof(RNG512) });
        private static readonly MethodInfo playSound = AccessTools.Method(typeof(MainManager), nameof(MainManager.PlaySound), new[] { typeof(string) });
        private static readonly MethodInfo rangeFloat = AccessTools.Method(typeof(UnityEngine.Random), "Range", new[] { typeof(float), typeof(float) });
        private static readonly MethodInfo rangeInt = AccessTools.Method(typeof(UnityEngine.Random), "Range", new[] { typeof(int), typeof(int) });
        private static readonly MethodInfo replacementRangeFloat = AccessTools.Method(typeof(RNG512), "RangeFloat");
        private static readonly MethodInfo replacementRangeInt = AccessTools.Method(typeof(RNG512), "RangeInt");
        private static readonly FieldInfo textholder = AccessTools.Field(typeof(FlappyBee), "textholder");
        private static readonly MethodInfo callSetText = AccessTools.Method(typeof(PatchFlappyBeeRNG), nameof(SetSeedText));

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

                if (func[i].Calls(playSound) && func[i - 1].OperandIs("FBGameOver"))
                {
                    func.InsertRange(i + 2, getThis.Concat(getThis).Concat(new[]
                    {
                        new CodeInstruction(OpCodes.Ldfld, textholder),
                        new CodeInstruction(OpCodes.Call, callSetText),
                    }));
                }
            }

            return func;
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return TranspileWithThis(instructions, new[] { new CodeInstruction(OpCodes.Ldarg_0) });
        }

        private static void SetSeedText(FlappyBee fj, Transform th)
        {
            // (this uses 0.5 gray rather than 0.8 gray, but fixing that would require much more code)
            fj.StartCoroutine(MainManager.SetText(FlappyBee.args + "|center||color,5|Seed: " + fj.GetComponent<RNG512>().Seed, 1, null, false, true, new Vector3(-7f, 18f), Vector3.one, Vector3.one * 0.7f, th, null));
        }
    }

    [HarmonyPatch]
    public class PatchFlappyBeeIteratorRNG
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(FlappyBee).GetNestedTypes(AccessTools.all).Where((t) => !t.IsValueType).Select<Type, MethodBase>((t) => AccessTools.Method(t, "MoveNext"));
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            return PatchFlappyBeeRNG.TranspileWithThis(instructions, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(original.DeclaringType, "$this")),
            });
        }
    }
}
