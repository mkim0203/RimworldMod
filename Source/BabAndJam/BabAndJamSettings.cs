using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BabAndJam
{
    public enum NeedBarPosition
    {
        Bottom,
        Sides,
        SidesOutside
    }

    public class BabAndJamSettings : ModSettings
    {
        public bool showNeedBars = true;
        public NeedBarPosition needBarPosition = NeedBarPosition.Bottom;
        public bool enableCarriedFoodCommand = true;
        public bool enableForcedSleepCommand = true;
        public float hungerThresholdPercent = 30f;
        public int needBarUpdateIntervalMs = 1000;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref showNeedBars, "showNeedBars", true);
            Scribe_Values.Look(ref needBarPosition, "needBarPosition", NeedBarPosition.Bottom);
            Scribe_Values.Look(ref enableCarriedFoodCommand, "enableCarriedFoodCommand", true);
            Scribe_Values.Look(ref enableForcedSleepCommand, "enableForcedSleepCommand", true);
            Scribe_Values.Look(ref hungerThresholdPercent, "hungerThresholdPercent", 30f);
            Scribe_Values.Look(ref needBarUpdateIntervalMs, "needBarUpdateIntervalMs", 1000);
            hungerThresholdPercent = Mathf.Clamp(hungerThresholdPercent, 10f, 90f);
            needBarUpdateIntervalMs = BabAndJamMod.NormalizeUpdateInterval(needBarUpdateIntervalMs);
        }
    }

    public class BabAndJamMod : Mod
    {
        public static BabAndJamSettings Settings;

        public BabAndJamMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BabAndJamSettings>();
        }

        public override string SettingsCategory()
        {
            return Content.Name;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("허기·수면 게이지 표시", ref Settings.showNeedBars,
                "상단 주민 초상화 안에 허기와 수면 게이지를 표시합니다.");
            if (Settings.showNeedBars && listing.ButtonText("게이지 표시 위치: " + PositionLabel(Settings.needBarPosition)))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("하단", () => Settings.needBarPosition = NeedBarPosition.Bottom),
                    new FloatMenuOption("사이드 (왼쪽 허기 / 오른쪽 수면)", () => Settings.needBarPosition = NeedBarPosition.Sides),
                    new FloatMenuOption("사이드 외부 (왼쪽 허기 / 오른쪽 수면)", () => Settings.needBarPosition = NeedBarPosition.SidesOutside)
                }));
            }
            if (Settings.showNeedBars && listing.ButtonText("게이지 수치 갱신 주기: " + Settings.needBarUpdateIntervalMs + "ms"))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("500ms", () => Settings.needBarUpdateIntervalMs = 500),
                    new FloatMenuOption("1000ms", () => Settings.needBarUpdateIntervalMs = 1000),
                    new FloatMenuOption("2000ms", () => Settings.needBarUpdateIntervalMs = 2000),
                    new FloatMenuOption("3000ms", () => Settings.needBarUpdateIntervalMs = 3000)
                }));
            }

            listing.GapLine();
            listing.CheckboxLabeled("휴대 식량 섭취 명령 사용", ref Settings.enableCarriedFoodCommand,
                "소집 중인 주민이 휴대 식량을 먹는 명령을 표시합니다.");
            if (Settings.enableCarriedFoodCommand)
            {
                listing.Label("식사 명령 표시 허기: " + Mathf.RoundToInt(Settings.hungerThresholdPercent) + "%");
                Settings.hungerThresholdPercent = Mathf.Round(listing.Slider(Settings.hungerThresholdPercent, 10f, 90f));
                listing.Label("허기가 이 값 미만일 때 휴대 식량 섭취 명령이 표시됩니다. (10~90%)");
            }

            listing.GapLine();
            listing.CheckboxLabeled("강제 수면 명령 사용", ref Settings.enableForcedSleepCommand,
                "비소집 상태의 주민에게 강제 수면 명령을 표시합니다.");
            listing.End();
        }

        private static string PositionLabel(NeedBarPosition position)
        {
            if (position == NeedBarPosition.SidesOutside)
            {
                return "사이드 외부 (왼쪽 허기 / 오른쪽 수면)";
            }

            return position == NeedBarPosition.Sides ? "사이드 (왼쪽 허기 / 오른쪽 수면)" : "하단";
        }

        public static int NormalizeUpdateInterval(int value)
        {
            return value == 500 || value == 1000 || value == 2000 || value == 3000 ? value : 1000;
        }
    }
}
