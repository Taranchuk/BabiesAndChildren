using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BabiesAndChildren.api;
using BabiesAndChildren.Tools;
using HarmonyLib;
using UnityEngine;
using Verse;
namespace BabiesAndChildren.Harmony
{
    public static class ChildrenSizePatch
    {
        public static void Patch()
        {
            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("RimWorld.babies.and.children." + nameof(ChildrenSizePatch));

            harmony.Patch(AccessTools.Method(typeof(PawnRenderer), name: "DrawPawnBody"), transpiler:
                          new HarmonyMethod(typeof(ChildrenSizePatch), nameof(DrawPawnBodyTranspiler)));
            harmony.Patch(AccessTools.Method(typeof(PawnRenderer), name: "DrawHeadHair"), transpiler:
                          new HarmonyMethod(typeof(ChildrenSizePatch), nameof(DrawHeadHairTranspiler)));
            harmony.Patch(AccessTools.Method(typeof(PawnRenderer), name: "RenderPawnInternal",
                                             new[] { typeof(Vector3), typeof(float), typeof(bool), typeof(Rot4), typeof(RotDrawMode), typeof(PawnRenderFlags) }), transpiler:
                          new HarmonyMethod(typeof(ChildrenSizePatch), nameof(RenderPawnInternalTranspiler)));
        }

        public static IEnumerable<CodeInstruction> DrawHeadHairTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo hairInfo = AccessTools.Property(typeof(PawnGraphicSet), nameof(PawnGraphicSet.HairMeshSet)).GetGetMethod();

            List<CodeInstruction> instructionList = instructions.ToList();


            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                if (i + 5 < instructionList.Count && instructionList[i + 2].OperandIs(hairInfo))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_S, 7) { labels = instruction.ExtractLabels() }; // renderFlags
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PawnRenderer), name: "pawn"));
                    yield return new CodeInstruction(OpCodes.Ldarg_S, operand: 5); //headfacing
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PawnRenderer), nameof(PawnRenderer.graphics)));
                    instruction = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChildrenSizePatch), nameof(PawnRenderer_DrawHairHead_Patch)));
                    i += 5;
                }

                yield return instruction;
            }
        }

        public static IEnumerable<CodeInstruction> DrawPawnBodyTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo humanlikeBodyInfo = AccessTools.Field(typeof(MeshPool), nameof(MeshPool.humanlikeBodySet));

            List<CodeInstruction> instructionList = instructions.ToList();

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];
                if (instruction.OperandIs(humanlikeBodyInfo))
                {
                    instructionList.RemoveRange(i, count: 2);
                    yield return new CodeInstruction(OpCodes.Ldarg_S, operand: 5); // renderFlags
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PawnRenderer), name: "pawn"));
                    yield return new CodeInstruction(OpCodes.Ldarg_3); // facing
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    instruction = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChildrenSizePatch), nameof(PawnRenderer_DrawPawnBody_Patch)));
                }
                yield return instruction;
            }
        }

        public static IEnumerable<CodeInstruction> RenderPawnInternalTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo humanlikeHeadInfo = AccessTools.Field(typeof(MeshPool), nameof(MeshPool.humanlikeHeadSet));
            MethodInfo drawHeadHairInfo = AccessTools.Method(typeof(PawnRenderer), "DrawHeadHair");
            MethodInfo flagSetInfo = AccessTools.Method(typeof(PawnRenderFlagsExtension), nameof(PawnRenderFlagsExtension.FlagSet));

            List<CodeInstruction> instructionList = instructions.ToList();

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];


                if (instruction.OperandIs(humanlikeHeadInfo))
                {
                    instructionList.RemoveRange(i, count: 2);
                    yield return new CodeInstruction(OpCodes.Ldarg_S, operand: 6); // renderFlags
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PawnRenderer), name: "pawn"));
                    yield return new CodeInstruction(OpCodes.Ldloc_S, operand: 7); //headfacing
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                    instruction = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChildrenSizePatch), nameof(PawnRenderer_DrawPawnBody_Patch)));
                }

                yield return instruction;
            }
        }

        public static Mesh PawnRenderer_DrawPawnBody_Patch(PawnRenderFlags renderFlags, Pawn pawn, Rot4 facing, bool wantsBody)
        {
                if (pawn != null &&
                    RaceUtility.PawnUsesChildren(pawn) &&
                    AgeStages.IsAgeStage(pawn, AgeStages.Child))
                {
                    if (wantsBody)
                    {
                        return AlienRacePatches.GetModifiedBodyMeshSet(ChildrenUtility.GetBodySize(pawn), pawn).MeshAt(facing);                
                    }
                    else
                    {
                        return AlienRacePatches.GetModifiedHeadMeshSet(ChildrenUtility.GetHeadSize(pawn), pawn).MeshAt(facing);
                    }
                }

            return MeshPool.humanlikeBodySet.MeshAt(facing);
        }

        public static Mesh PawnRenderer_DrawHairHead_Patch(PawnRenderFlags renderFlags, Pawn pawn, Rot4 headFacing, PawnGraphicSet graphics)
        {
            if (pawn == null ||
                !RaceUtility.PawnUsesChildren(pawn) ||
                !AgeStages.IsAgeStage(pawn, AgeStages.Child))
                return graphics.HairMeshSet.MeshAt(headFacing);

            float hairSizeFactor = ChildrenUtility.GetHairSize(0, pawn);
            return AlienRacePatches.GetModifiedHairMeshSet(hairSizeFactor, pawn).MeshAt(headFacing);
        }


    }

    [HarmonyPatch(typeof(PawnRenderer), "CarryWeaponOpenly")]
    public static class PawnRenderer_CarryWeaponOpenly_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref PawnGraphicSet __instance, ref bool __result)
        {
            if (__instance.pawn.equipment.Primary != null &&
                ChildrenUtility.SetMakerTagCheck(__instance.pawn.equipment.Primary, "Toy"))
            {
                __result = true;
            }

        }
    }


    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnInternal")]
    public static class PawnRenderer_RenderPawnInternal_Patch
    {
        [HarmonyBefore(new string[] { "rimworld.Nals.FacialAnimation" })]
        static bool Prefix(PawnRenderer __instance, ref Vector3 rootLoc)
        {

            //if (!(__instance.graphics.pawn is Pawn ___pawn) || renderBody == false ) return false;
            Pawn ___pawn = __instance.graphics.pawn;
            if (!RaceUtility.PawnUsesChildren(___pawn)) return true;
            // Change the root location of the child's draw position
            if (AgeStages.IsYoungerThan(___pawn, AgeStages.Teenager))
                rootLoc = GraphicTools.ModifyChildYPosOffset(rootLoc, ___pawn);
            //__instance.RenderPawnInternal(rootLoc, angle, true, bodyFacing, bodyDrawType, flags );

            //facial animation compatibility for babies and toddlers
            if (ChildrenBase.ModFacialAnimation_ON && AgeStages.IsYoungerThan(___pawn, AgeStages.Child))
                ModTools.RemoveFacialAnimationComps(___pawn);

            return true;
            // The rest of the child Pawn renderering is done by alienrace mod.
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "BaseHeadOffsetAt")]
    [HarmonyAfter(new string[] { "AlienRace" })]
    public static class PawnRenderer_BaseHeadOffsetAt_Patch
    {
        [HarmonyPostfix]
        public static void BaseHeadOffsetAtPostfix_Post(PawnRenderer __instance, Rot4 rotation, ref Vector3 __result, ref Pawn ___pawn)
        {
            Pawn pawn = null;
            try
            {
                pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            }
            catch
            {
                CLog.Warning("PawnRenderer instance has null pawn.");
                return;
            }

            if (pawn == null || 
                !RaceUtility.PawnUsesChildren(pawn) ||
                !AgeStages.IsAgeStage(pawn, AgeStages.Child)) return;
            
            float bodySizeFactor = ChildrenUtility.GetBodySize(pawn);
            float num2 = 1f;
            float num3 = 1f;

            if (pawn.def.defName == "Alien_Orassan")
            {
                num2 = 1.4f;
                num3 = 1.4f;
            }
            __result.z *= bodySizeFactor * num2;
            __result.x *= bodySizeFactor * num3;
            if (RaceUtility.IsHuman(pawn))
            {
                __result.z += Tweaks.HuHeadlocZ ; 
            }
            if (ChildrenBase.ModFacialAnimation_ON)
            {
                __result += GraphicTools.ModifyChildYPosOffset(Vector3.zero, ___pawn);
            }
        }
    }
}