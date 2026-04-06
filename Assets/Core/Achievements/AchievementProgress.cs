using System;
using System.Collections.Generic;
using UnityEngine;
using StaticDrift.Cards;
using StaticDrift.Managers;

namespace StaticDrift.Achievements
{
    /// <summary>Local persistence and unlock rules for achievements.</summary>
    public static class AchievementProgress
    {
        /// <summary>Fired once when an achievement becomes newly unlocked (after save). Subscribe from HUD only for in-run feedback.</summary>
        public static event Action<AchievementId> NewlyUnlocked;

        public const int Count = 15;

        private const string KeyMask = "Achv_UnlockedMask";
        private const string KeyAsteroidsLifetime = "Achv_AsteroidsLifetime";
        private const string KeyHostilesLifetime = "Achv_HostilesLifetime";
        private const string KeyEliteWavesLifetime = "Achv_EliteWavesLifetime";
        private const string KeyWrappedKills = "Achv_WrappedEdgeAsteroids";
        private const string KeyPersonalBestBreaks = "Achv_PersonalBestBreaks";

        private const int BeltBreakerTarget = 500;
        private const int AcesHighTarget = 40;
        private const int EliteHunterTarget = 5;
        private const int SectorRecordTarget = 5;
        private const int WrappedTarget = 25;

        private static readonly string[] Titles =
        {
            "Clean Sector",
            "Drift Survivor",
            "Aces High",
            "Belt Breaker",
            "Chain Reaction",
            "Elite Hunter",
            "Cutting It Close",
            "Family Ties",
            "Quad Core",
            "Volley Storm",
            "Full Deck",
            "Deep Run",
            "Sector Record",
            "Top of the Pile",
            "Wrapped"
        };

        private static readonly string[] Descriptions =
        {
            "Finish a timed wave without taking damage.",
            "Complete wave 10 or higher in a single run.",
            "Destroy 40 hostile craft across all runs.",
            "Destroy 500 asteroids across all runs.",
            "Destroy 3 asteroids within 2 seconds.",
            "Survive 5 elite waves across all runs.",
            "Finish a timed wave with 3 seconds or less left on the clock.",
            "Hold 6+ upgrade stacks in one of Volt, Kinetic, Thermal, or Static in a single run.",
            "Have Volt, Kinetic, Thermal, and Static all present in your loadout at once.",
            "Reach the maximum volley spread (4 shots) in a single run.",
            "Fill all 4 loadout slots in a single run.",
            "Complete wave 15 or higher in a single run.",
            "Set a new personal best score 5 times.",
            "Reach #1 on the local high score board.",
            "Destroy 25 asteroids while flying near the screen edge."
        };

        private static readonly int[] ProgressCaps =
        {
            0,
            0,
            AcesHighTarget,
            BeltBreakerTarget,
            0,
            EliteHunterTarget,
            0,
            0,
            0,
            0,
            0,
            0,
            SectorRecordTarget,
            0,
            WrappedTarget
        };

        public static string GetTitle(AchievementId id) => Titles[(int)id];

        public static string GetDescription(AchievementId id) => Descriptions[(int)id];

        public static int GetProgressCap(AchievementId id) => ProgressCaps[(int)id];

        public static bool IsUnlocked(AchievementId id)
        {
            int mask = PlayerPrefs.GetInt(KeyMask, 0);
            return (mask & (1 << (int)id)) != 0;
        }

        public static int GetProgressValue(AchievementId id)
        {
            switch (id)
            {
                case AchievementId.AcesHigh:
                    return Mathf.Min(AcesHighTarget, PlayerPrefs.GetInt(KeyHostilesLifetime, 0));
                case AchievementId.BeltBreaker:
                    return Mathf.Min(BeltBreakerTarget, PlayerPrefs.GetInt(KeyAsteroidsLifetime, 0));
                case AchievementId.EliteHunter:
                    return Mathf.Min(EliteHunterTarget, PlayerPrefs.GetInt(KeyEliteWavesLifetime, 0));
                case AchievementId.SectorRecord:
                    return Mathf.Min(SectorRecordTarget, PlayerPrefs.GetInt(KeyPersonalBestBreaks, 0));
                case AchievementId.Wrapped:
                    return Mathf.Min(WrappedTarget, PlayerPrefs.GetInt(KeyWrappedKills, 0));
                default:
                    return IsUnlocked(id) ? 1 : 0;
            }
        }

        public static void RecordAsteroidDestroyed(bool playerNearWrapEdge)
        {
            int n = PlayerPrefs.GetInt(KeyAsteroidsLifetime, 0) + 1;
            PlayerPrefs.SetInt(KeyAsteroidsLifetime, n);
            if (playerNearWrapEdge)
            {
                int w = PlayerPrefs.GetInt(KeyWrappedKills, 0) + 1;
                PlayerPrefs.SetInt(KeyWrappedKills, w);
                TryProgressUnlock(AchievementId.Wrapped, w, WrappedTarget);
            }

            TryProgressUnlock(AchievementId.BeltBreaker, n, BeltBreakerTarget);
            PlayerPrefs.Save();
        }

        public static void RecordHostileDestroyed()
        {
            int n = PlayerPrefs.GetInt(KeyHostilesLifetime, 0) + 1;
            PlayerPrefs.SetInt(KeyHostilesLifetime, n);
            TryProgressUnlock(AchievementId.AcesHigh, n, AcesHighTarget);
            PlayerPrefs.Save();
        }

        public static void RecordEliteWaveSurvived()
        {
            int n = PlayerPrefs.GetInt(KeyEliteWavesLifetime, 0) + 1;
            PlayerPrefs.SetInt(KeyEliteWavesLifetime, n);
            TryProgressUnlock(AchievementId.EliteHunter, n, EliteHunterTarget);
            PlayerPrefs.Save();
        }

        public static void OnGameOverScore(int finalScore, IReadOnlyList<int> topScoresAfterSave)
        {
            int bestBefore = ReadBestStoredScore();
            if (finalScore > bestBefore)
            {
                int breaks = PlayerPrefs.GetInt(KeyPersonalBestBreaks, 0) + 1;
                PlayerPrefs.SetInt(KeyPersonalBestBreaks, breaks);
                TryProgressUnlock(AchievementId.SectorRecord, breaks, SectorRecordTarget);
            }

            if (topScoresAfterSave != null && topScoresAfterSave.Count > 0 && topScoresAfterSave[0] == finalScore)
            {
                Unlock(AchievementId.TopOfThePile);
            }

            PlayerPrefs.Save();
        }

        private static int ReadBestStoredScore()
        {
            const string key = "TopScores";
            string raw = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return 0;
            }

            string[] parts = raw.Split(',');
            int best = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                int parsed;
                if (int.TryParse(parts[i], out parsed) && parsed > best)
                {
                    best = parsed;
                }
            }

            return best;
        }

        public static void TryUnlockChainReaction(float nowUnscaledTime, List<float> recentAsteroidDestroyTimes)
        {
            if (recentAsteroidDestroyTimes == null)
            {
                return;
            }

            recentAsteroidDestroyTimes.Add(nowUnscaledTime);
            const float window = 2f;
            for (int i = recentAsteroidDestroyTimes.Count - 1; i >= 0; i--)
            {
                if (nowUnscaledTime - recentAsteroidDestroyTimes[i] > window)
                {
                    recentAsteroidDestroyTimes.RemoveAt(i);
                }
            }

            if (recentAsteroidDestroyTimes.Count >= 3)
            {
                Unlock(AchievementId.ChainReaction);
            }
        }

        public static void Unlock(AchievementId id)
        {
            int mask = PlayerPrefs.GetInt(KeyMask, 0);
            int bit = 1 << (int)id;
            if ((mask & bit) != 0)
            {
                return;
            }

            mask |= bit;
            PlayerPrefs.SetInt(KeyMask, mask);
            PlayerPrefs.Save();
            NewlyUnlocked?.Invoke(id);
        }

        private static void TryProgressUnlock(AchievementId id, int value, int target)
        {
            if (value >= target)
            {
                Unlock(id);
            }
        }

        public static void EvaluateRunUpgradeAchievements(RunUpgradeController run)
        {
            if (run == null)
            {
                return;
            }

            if (run.VolleyPelletCount >= 4)
            {
                Unlock(AchievementId.VolleyStorm);
            }

            if (run.LoadoutTags.Count >= RunUpgradeController.LoadoutSlotCount)
            {
                Unlock(AchievementId.FullDeck);
            }

            IReadOnlyList<CardTag> tags = run.LoadoutTags;
            if (ContainsTag(tags, CardTag.Volt)
                && ContainsTag(tags, CardTag.Kinetic)
                && ContainsTag(tags, CardTag.Thermal)
                && ContainsTag(tags, CardTag.Static))
            {
                Unlock(AchievementId.QuadCore);
            }

            if (GetMaxCoreFamilyStacks(run) >= 6)
            {
                Unlock(AchievementId.FamilyTies);
            }
        }

        private static int GetMaxCoreFamilyStacks(RunUpgradeController run)
        {
            int max = 0;
            max = Mathf.Max(max, run.GetTotalStacksForTag(CardTag.Volt));
            max = Mathf.Max(max, run.GetTotalStacksForTag(CardTag.Kinetic));
            max = Mathf.Max(max, run.GetTotalStacksForTag(CardTag.Thermal));
            max = Mathf.Max(max, run.GetTotalStacksForTag(CardTag.Static));
            return max;
        }

        private static bool ContainsTag(IReadOnlyList<CardTag> tags, CardTag need)
        {
            int n = tags.Count;
            for (int i = 0; i < n; i++)
            {
                if (tags[i] == need)
                {
                    return true;
                }
            }

            return false;
        }

        public static string BuildStatusLine(AchievementId id)
        {
            string title = GetTitle(id);
            if (IsUnlocked(id))
            {
                return "<color=#7CFFB0>✓</color> " + title;
            }

            int cap = GetProgressCap(id);
            if (cap > 0)
            {
                int v = GetProgressValue(id);
                return "○ " + title + "  (" + v + "/" + cap + ")";
            }

            return "○ " + title;
        }

        public static string BuildScrollBodyText(int descriptionRichSizePx = 32)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(2048);
            string sizeTag = "<size=" + descriptionRichSizePx + ">";
            for (int i = 0; i < Count; i++)
            {
                AchievementId id = (AchievementId)i;
                sb.AppendLine(BuildStatusLine(id));
                sb.AppendLine(sizeTag + "<color=#AAB8CC>" + GetDescription(id) + "</color></size>");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
