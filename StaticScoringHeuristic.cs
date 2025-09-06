using MassacreStackFinderCs.Types;

namespace MassacreStackFinderCs;

public static class StaticScoringHeuristic
{
    // Calculate the base mission giver score of a system. Does not account for number of factions present in the system
    public static float CalculateMissionGiverScore(StarSystem system)
    {
        // If there's no source station to pick up missions from, score is zero
        Station? station = system.MissionSourceStation;
        if (station == null)
        {
            return 0.0f;
        }

        // Award double score for military economy on the source station
        float baseFactor = (station?.IsMilitaryEconomy ?? false) ? 2.0f : 1.0f; 

        int numTargetSystems = system.MissionTargetSystems.Count();

        // If there's no targets, score is zero
        if (numTargetSystems == 0)
        {
            return 0.0f;
        }

        // Yield maximum score if there's just one potential target system, reduce score if there's more
        // Pow is used here to match highly detrimental effect of multiple nearby targets
        return baseFactor / MathF.Pow(numTargetSystems, 1.7f);
    }

    // Calculate and set a target systems score
    public static void CalculateTargetSystemScore(MassacreTargetSystem targetSystem)
    {
        // Enumerate mission giver systems
        foreach (var missionGiverSystem in targetSystem.MissionGiverSystems)
        {
            float systemScore = missionGiverSystem.MissionGiverScore;
            
            // Update target factions, assign sum of mission giver scores for each system they're in
            foreach (var faction in missionGiverSystem.NonAnarchyFactions)
            {
                if (targetSystem.Factions.TryGetValue(faction, out float oldScore))
                {
                    targetSystem.Factions[faction] = oldScore + systemScore;
                }
                else
                {
                    targetSystem.Factions[faction] = systemScore;
                }
            }
        }

        // Rescale scores if they're >1, because a super high score for a single faction isn't all that valuable
        foreach (var kvp in targetSystem.Factions)
        {
            targetSystem.Factions[kvp.Key] = kvp.Value > 1 ? MathF.Sqrt(kvp.Value) : kvp.Value;
        }

        // Calculate final score by sum of faction scores, clamped to [0, NumFactions]
        targetSystem.Score = Math.Clamp(targetSystem.Factions.Values.Sum(), 0, targetSystem.Factions.Count);
    }
}