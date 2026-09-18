using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BabAndJam
{
    [StaticConstructorOnStartup]
    public static class BabAndJamStartup
    {
        static BabAndJamStartup()
        {
            new Harmony("mhkim.babandjam").PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    // DrawColonist supplies the exact, already-scaled portrait rectangle, including when the
    // colonist bar changes its layout because of the number of pawns on screen.
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.DrawColonist))]
    public static class ColonistBarColonistDrawer_DrawColonist_Patch
    {
        private struct NeedBarValues
        {
            public float Food;
            public float Rest;
            public float UpdatedAt;
        }

        private static readonly Color EmptyBarColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color FoodColor = new Color(0.95f, 0.54f, 0.10f);
        private static readonly Color RestColor = new Color(0.20f, 0.62f, 0.96f);
        private static readonly Dictionary<int, NeedBarValues> CachedNeedValues = new Dictionary<int, NeedBarValues>();

        public static void Postfix(Rect rect, Pawn colonist)
        {
            if (colonist == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (colonist.needs != null && BabAndJamMod.Settings?.showNeedBars != false)
            {
                NeedBarValues values = GetNeedBarValues(colonist);
                float margin = Mathf.Max(1f, rect.width * 0.05f);
                if (BabAndJamMod.Settings?.needBarPosition == NeedBarPosition.Sides)
                {
                    DrawSideBars(rect, margin, values);
                }
                else if (BabAndJamMod.Settings?.needBarPosition == NeedBarPosition.SidesOutside)
                {
                    DrawOutsideSideBars(rect, margin, values);
                }
                else
                {
                    DrawBottomBars(rect, margin, values);
                }
            }

            //if (colonist.Drafted && NeedsOnColonistBarMod.Settings?.showDrugBuffs != false)
            //{
            //    DrawActiveDrugIcons(rect, colonist);
            //}
        }

        private static NeedBarValues GetNeedBarValues(Pawn colonist)
        {
            float now = Time.realtimeSinceStartup;
            int intervalMs = BabAndJamMod.Settings?.needBarUpdateIntervalMs ?? 1000;
            float intervalSeconds = intervalMs / 1000f;
            NeedBarValues values;

            if (!CachedNeedValues.TryGetValue(colonist.thingIDNumber, out values) ||
                now - values.UpdatedAt >= intervalSeconds)
            {
                values.Food = Mathf.Clamp01(colonist.needs.food?.CurLevelPercentage ?? 0f);
                values.Rest = Mathf.Clamp01(colonist.needs.rest?.CurLevelPercentage ?? 0f);
                values.UpdatedAt = now;
                CachedNeedValues[colonist.thingIDNumber] = values;
            }

            return values;
        }

        private static void DrawBottomBars(Rect portraitRect, float margin, NeedBarValues values)
        {
            float barHeight = Mathf.Clamp(portraitRect.height * 0.07f, 2f, 4f);
            float gap = Mathf.Max(1f, barHeight * 0.5f);
            float bottom = portraitRect.yMax - margin;
            Rect foodRect = new Rect(portraitRect.x + margin, bottom - barHeight * 2f - gap,
                portraitRect.width - margin * 2f, barHeight);
            Rect restRect = new Rect(portraitRect.x + margin, bottom - barHeight,
                portraitRect.width - margin * 2f, barHeight);

            DrawNeedBar(foodRect, values.Food, FoodColor, false);
            DrawNeedBar(restRect, values.Rest, RestColor, false);
        }

        private static void DrawSideBars(Rect portraitRect, float margin, NeedBarValues values)
        {
            float barWidth = Mathf.Clamp(portraitRect.width * 0.08f, 2f, 5f);
            float barHeight = portraitRect.height - margin * 2f;
            Rect foodRect = new Rect(portraitRect.x + margin, portraitRect.y + margin, barWidth, barHeight);
            Rect restRect = new Rect(portraitRect.xMax - margin - barWidth, portraitRect.y + margin, barWidth, barHeight);

            DrawNeedBar(foodRect, values.Food, FoodColor, true);
            DrawNeedBar(restRect, values.Rest, RestColor, true);
        }

        private static void DrawOutsideSideBars(Rect portraitRect, float margin, NeedBarValues values)
        {
            float barWidth = Mathf.Clamp(portraitRect.width * 0.08f, 2f, 5f);
            float barHeight = portraitRect.height - margin * 2f;
            // Attach directly to the portrait border without an outside gap.
            Rect foodRect = new Rect(portraitRect.x - barWidth, portraitRect.y + margin, barWidth, barHeight);
            Rect restRect = new Rect(portraitRect.xMax, portraitRect.y + margin, barWidth, barHeight);

            DrawNeedBar(foodRect, values.Food, FoodColor, true);
            DrawNeedBar(restRect, values.Rest, RestColor, true);
        }

        private static void DrawNeedBar(Rect rect, float percent, Color fillColor, bool vertical)
        {
            if (rect.width <= 0f)
            {
                return;
            }

            Widgets.DrawBoxSolid(rect, EmptyBarColor);
            Rect filled = rect;
            percent = Mathf.Clamp01(percent);
            if (vertical)
            {
                filled.height *= percent;
                filled.y = rect.yMax - filled.height;
            }
            else
            {
                filled.width *= percent;
            }
            Widgets.DrawBoxSolid(filled, fillColor);
        }

        private static void DrawActiveDrugIcons(Rect portraitRect, Pawn colonist)
        {
            List<Hediff> activeDrugHighs = colonist.health?.hediffSet?.hediffs
                .Where(hediff => hediff is Hediff_High)
                .ToList();
            if (activeDrugHighs == null || activeDrugHighs.Count == 0)
            {
                return;
            }

            float iconSize = Mathf.Clamp(portraitRect.width * 0.28f, 12f, 20f);
            float totalWidth = activeDrugHighs.Count * iconSize;
            float startX = portraitRect.center.x - totalWidth * 0.5f;
            float iconY = Mathf.Max(0f, portraitRect.y - iconSize - 2f);

            for (int index = 0; index < activeDrugHighs.Count; index++)
            {
                Hediff high = activeDrugHighs[index];
                Texture2D icon = FindDrugIcon(high);
                if (icon == null)
                {
                    continue;
                }

                Rect iconRect = new Rect(startX + index * iconSize, iconY, iconSize, iconSize);
                GUI.DrawTexture(iconRect, icon);
                TooltipHandler.TipRegion(iconRect, high.LabelCap);
            }
        }

        private static Texture2D FindDrugIcon(Hediff high)
        {
            ThingDef drugDef = DefDatabase<ThingDef>.AllDefs.FirstOrDefault(def =>
                def.ingestible?.drugCategory != DrugCategory.None &&
                def.ingestible.outcomeDoers?.OfType<IngestionOutcomeDoer_GiveHediff>()
                    .Any(doer => doer.hediffDef == high.def) == true);
            return drugDef?.uiIcon;
        }

    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Pawn_GetGizmos_Patch
    {
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            BabAndJamSettings settings = BabAndJamMod.Settings;
            float hungerThreshold = (settings?.hungerThresholdPercent ?? 30f) / 100f;

            if (__instance == null || !__instance.Drafted || __instance.needs?.food == null ||
                settings?.enableCarriedFoodCommand == false ||
                __instance.needs.food.CurLevelPercentage >= hungerThreshold)
            {
                return;
            }

            Thing food = FindCarriedFood(__instance);
            if (food == null)
            {
                return;
            }

            Command_Action eatCarriedFood = new Command_Action
            {
                defaultLabel = "휴대 식량 섭취",
                defaultDesc = "소집 중 들고 있는 식량 한 개를 즉시 먹습니다.",
                icon = ThingDefOf.MealSurvivalPack.uiIcon,
                action = delegate
                {
                    EatOneCarriedFood(__instance);
                }
            };

            List<Gizmo> gizmos = __result.ToList();
            int undraftCommandIndex = gizmos.FindIndex(gizmo =>
                gizmo is Command_Toggle command && command.defaultLabel == "CommandUndraft".Translate());

            // Gizmos are drawn in enumeration order, so insert directly before vanilla Undraft.
            gizmos.Insert(undraftCommandIndex >= 0 ? undraftCommandIndex : 0, eatCarriedFood);
            __result = gizmos;
        }

        private static Thing FindCarriedFood(Pawn pawn)
        {
            if (pawn.inventory?.innerContainer == null)
            {
                return null;
            }

            return pawn.inventory.innerContainer.FirstOrDefault(thing =>
                thing.IngestibleNow && FoodUtility.WillEat(pawn, thing, pawn, false));
        }

        private static void EatOneCarriedFood(Pawn pawn)
        {
            Thing food = FindCarriedFood(pawn);
            if (food == null)
            {
                Messages.Message("먹을 수 있는 휴대 식량이 없습니다.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            // Let RimWorld's normal JobDriver_Ingest handle the entire action. This preserves
            // the eating duration, interruption rules, food thoughts and the one-item stack use.
            Job eatJob = JobMaker.MakeJob(JobDefOf.Ingest, food);
            eatJob.count = 1;
            eatJob.playerForced = true;
            pawn.jobs.TryTakeOrderedJob(eatJob, JobTag.Misc);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Pawn_GetGizmos_ForcedSleep_Patch
    {
        private static readonly FieldInfo SleepingIconField =
            AccessTools.Field(typeof(ColonistBarColonistDrawer), "Icon_Sleeping");

        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance == null || !__instance.IsColonistPlayerControlled || __instance.Drafted ||
                BabAndJamMod.Settings?.enableForcedSleepCommand == false)
            {
                return;
            }

            Command_Action forceSleep = new Command_Action
            {
                defaultLabel = "강제 수면",
                defaultDesc = "자신의 침대로 가서 잠듭니다. 소유 침대가 없으면 현재 위치에서 잠듭니다.",
                // Reuse the exact Z icon that vanilla draws for a sleeping colonist.
                icon = (Texture2D)SleepingIconField.GetValue(null),
                action = delegate
                {
                    StartForcedSleep(__instance);
                }
            };

            __result = __result.Concat(new Gizmo[] { forceSleep });
        }

        private static void StartForcedSleep(Pawn pawn)
        {
            Building_Bed ownedBed = pawn.ownership?.OwnedBed;
            Job sleepJob = ownedBed != null && ownedBed.Spawned
                ? JobMaker.MakeJob(JobDefOf.LayDown, ownedBed)
                : JobMaker.MakeJob(JobDefOf.LayDown, pawn.Position);

            sleepJob.playerForced = true;
            pawn.jobs.TryTakeOrderedJob(sleepJob, JobTag.Misc);
        }
    }
}
