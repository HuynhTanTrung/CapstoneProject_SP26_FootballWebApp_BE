using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.Services;

/// <summary>
/// VN Player Rating System v3.3
/// Base: 6.0 | Scale: 1.0–10.0 | Condition: ≥ 1 minute played
/// matchResult: "W" = win, "D" = draw, "L" = loss, null = unknown
/// </summary>
public static class PlayerRatingCalculator
{
    public static decimal? Calculate(PlayerMatchStatistic s, string? position, string? matchResult = null)
    {
        if (s.Minutes == null || s.Minutes < 1) return null;

        var pos = NormalizePosition(position);

        // Build RawRating per spec section II
        double raw = 6.0;
        raw += AttackScore(s, pos);
        raw += PassingScore(s, pos);
        raw += DefenseScore(s, pos);
        raw += DuelScore(s, pos);
        raw += DribblingScore(s, pos);
        if (pos == "G") raw += GKScore(s);
        raw -= NegativeScore(s);
        raw -= CardPenalty(s, pos);

        // TeamResultBonus: Fix 6 checks raw BEFORE adding bonus
        raw += TeamResultBonus(matchResult, raw);

        double final = NormalizeByMinutes(raw, s.Minutes.Value, s);
        final = Math.Clamp(final, 1.0, 10.0);
        return (decimal)Math.Round(final, 2);
    }

    // ── Position ─────────────────────────────────────────────────────────────
    public static string NormalizePosition(string? pos)
    {
        if (string.IsNullOrEmpty(pos)) return "M";
        return pos.ToUpper() switch
        {
            "G" or "GK" or "GOALKEEPER" => "G",
            "D" or "CB" or "LB" or "RB" or "WB" or "DEFENDER" => "D",
            "M" or "CM" or "DM" or "AM" or "LM" or "RM" or "MIDFIELDER" => "M",
            "F" or "FW" or "ST" or "CF" or "LW" or "RW" or "FORWARD" or "ATTACKER" => "F",
            _ => "M"
        };
    }

    // ── VI. Attack (cap: +2.5) ───────────────────────────────────────────────
    private static double AttackScore(PlayerMatchStatistic s, string pos)
    {
        double score = 0;

        // Goals
        double goalPts = pos switch { "F" => 0.5, "M" => 0.7, "D" => 0.9, "G" => 1.8, _ => 0.5 };
        score += (s.Goals ?? 0) * goalPts;

        // Assists
        double assistPts = pos switch { "F" => 0.4, "M" => 0.5, "D" => 0.4, "G" => 0.8, _ => 0.4 };
        score += (s.Assists ?? 0) * assistPts;

        // Shots on target (not counting goals), Fix 2.4: ×0.7 if no goals
        if (pos != "G")
        {
            int sot = Math.Max(0, (s.ShotsOnTarget ?? 0) - (s.Goals ?? 0));
            double sotPts = pos switch { "F" => 0.10, "M" => 0.08, "D" => 0.08, _ => 0.08 };
            double sotCap = pos switch { "F" => 0.20, "M" => 0.16, "D" => 0.16, _ => 0.16 };
            double sotMult = (s.Goals ?? 0) == 0 ? 0.7 : 1.0;
            score += Math.Min(sot * sotPts * sotMult, sotCap);
        }

        // Key passes (not for GK)
        if (pos != "G")
        {
            double kpPts = pos switch { "F" => 0.08, "M" => 0.10, "D" => 0.08, _ => 0.08 };
            double kpCap = pos switch { "F" => 0.24, "M" => 0.30, "D" => 0.24, _ => 0.24 };
            score += Math.Min((s.PassesKey ?? 0) * kpPts, kpCap);
        }

        // xG diff: Clamp(xGDiff * 0.2, -0.3, +0.3)
        if (s.ExpectedGoals.HasValue)
        {
            double xgDiff = (s.Goals ?? 0) - (double)s.ExpectedGoals.Value;
            score += Math.Clamp(xgDiff * 0.2, -0.3, 0.3);
        }

        // xA diff: Clamp(xADiff * 0.15, -0.15, +0.15)
        if (s.ExpectedAssists.HasValue)
        {
            double xaDiff = (s.Assists ?? 0) - (double)s.ExpectedAssists.Value;
            score += Math.Clamp(xaDiff * 0.15, -0.15, 0.15);
        }

        // Penalties won (not for GK)
        if (pos != "G" && (s.PenaltiesWon ?? 0) > 0)
        {
            double pwPts = pos switch { "F" => 0.3, "M" => 0.25, "D" => 0.15, _ => 0.15 };
            score += s.PenaltiesWon!.Value * pwPts;
        }

        // Penalties missed: -0.6 each (all positions)
        score -= (s.PenaltiesMissed ?? 0) * 0.6;

        // Accurate crosses (cap: +0.2, not for GK)
        if (pos != "G")
        {
            double crossPts = pos switch { "F" => 0.06, "M" => 0.08, "D" => 0.06, _ => 0.06 };
            score += Math.Min((s.AccurateCrosses ?? 0) * crossPts, 0.2);
        }

        // Section XIV: Penalty shootout scoring (Vietnam Cup)
        // Sút thành công +0.3, sút hỏng -0.25
        if ((s.PenaltyShootoutScored ?? 0) > 0)
            score += (s.PenaltyShootoutScored!.Value * 0.3);
        if ((s.PenaltyShootoutMissed ?? 0) > 0)
            score -= (s.PenaltyShootoutMissed!.Value * 0.25);

        return Math.Min(score, 2.5);
    }

    // ── VII. Passing (cap: +0.8) ─────────────────────────────────────────────
    private static double PassingScore(PlayerMatchStatistic s, string pos)
    {
        int passes = s.Passes ?? 0;
        if (passes == 0) return 0;

        // PassAccuracyRate = PassesAccuracy (stored as 0–100 integer)
        double accuracy = (s.PassesAccuracy ?? 0) / 100.0;

        double baseScore = pos switch
        {
            "F" => accuracy >= 0.80 ? 0.2 : accuracy >= 0.65 ? 0.0 : -0.15,
            "M" => accuracy >= 0.88 ? 0.8 : accuracy >= 0.82 ? 0.5 : accuracy >= 0.75 ? 0.2 : accuracy >= 0.65 ? 0.0 : -0.5,
            "D" => accuracy >= 0.88 ? 0.5 : accuracy >= 0.78 ? 0.2 : accuracy >= 0.65 ? 0.0 : -0.4,
            "G" => accuracy >= 0.75 ? 0.2 : accuracy >= 0.55 ? 0.0 : -0.3,
            _ => 0
        };

        // Multipliers per spec: applied to positive baseScore only
        // Negative baseScore stays as-is (confirmed by spec examples in section XV)
        double score;
        if (baseScore > 0)
        {
            double volumeMult   = Math.Min(1.0, passes / 30.0);
            double keyPassMult  = (s.PassesKey ?? 0) == 0 ? 0.8 : 1.0;
            double diminishMult = passes > 60 ? 0.9 : 1.0;
            score = baseScore * volumeMult * keyPassMult * diminishMult;
        }
        else
        {
            score = baseScore;
        }

        // Accurate long balls (cap: +0.2) — M, D, G only
        double lbPts = pos switch { "M" => 0.05, "D" => 0.06, "G" => 0.08, _ => 0 };
        if (lbPts > 0)
            score += Math.Min((s.AccurateLongBalls ?? 0) * lbPts, 0.2);

        return Math.Min(score, 0.8);
    }

    // ── VIII. Defense (cap: +1.2) ────────────────────────────────────────────
    private static double DefenseScore(PlayerMatchStatistic s, string pos)
    {
        double score = 0;

        // Tackles won
        double tkPts = pos switch { "F" => 0.06, "M" => 0.10, "D" => 0.16, _ => 0.06 };
        double tkCap = pos switch { "F" => 0.18, "M" => 0.40, "D" => 0.64, _ => 0.18 };
        score += Math.Min((s.TacklesWon ?? 0) * tkPts, tkCap);

        // Interceptions
        double intPts = pos switch { "F" => 0.05, "M" => 0.08, "D" => 0.13, "G" => 0.08, _ => 0.05 };
        double intCap = pos switch { "F" => 0.15, "M" => 0.32, "D" => 0.52, "G" => 0.16, _ => 0.15 };
        score += Math.Min((s.Interceptions ?? 0) * intPts, intCap);

        // Clearances — D and G only
        if (pos == "D" || pos == "G")
        {
            double clrPts = pos == "D" ? 0.07 : 0.05;
            double clrCap = pos == "D" ? 0.28 : 0.15;
            score += Math.Min((s.Clearances ?? 0) * clrPts, clrCap);
        }

        // Blocks — M and D only
        if (pos == "M" || pos == "D")
        {
            double blkPts = pos == "M" ? 0.10 : 0.18;
            double blkCap = pos == "M" ? 0.20 : 0.36;
            score += Math.Min((s.Blocks ?? 0) * blkPts, blkCap);
        }

        // Ball recoveries (cap: +0.2)
        double recPts = pos switch { "F" => 0.03, "M" => 0.04, "D" => 0.05, _ => 0.03 };
        score += Math.Min((s.BallRecoveries ?? 0) * recPts, 0.2);

        // Dribbled past: Fix 4 — ignore first occurrence, cap -0.4
        if (pos == "M" || pos == "D")
        {
            int dribbledPast = Math.Max(0, (s.Tackles ?? 0) - (s.TacklesWon ?? 0));
            int effective = Math.Max(0, dribbledPast - 1);
            double dpPts = pos == "M" ? 0.06 : 0.08;
            score -= Math.Min(effective * dpPts, 0.4);
        }

        return Math.Min(score, 1.2);
    }

    // ── IX. Duels (cap: +0.4) ────────────────────────────────────────────────
    private static double DuelScore(PlayerMatchStatistic s, string pos)
    {
        double score = 0;

        // Ground duels
        int gTotal = (s.GroundDuelsWon ?? 0) + (s.GroundDuelsLost ?? 0);
        if (gTotal > 0)
        {
            double gRate = (double)(s.GroundDuelsWon ?? 0) / gTotal;
            score += gRate >= 0.65 ? 0.2 : gRate < 0.45 ? -0.1 : 0;
        }

        // Aerial duels
        int aTotal = (s.AerialDuelsWon ?? 0) + (s.AerialDuelsLost ?? 0);
        if (aTotal > 0)
        {
            double aRate = (double)(s.AerialDuelsWon ?? 0) / aTotal;
            double aWin  = pos switch { "F" => 0.12, "M" => 0.08, "D" => 0.15, "G" => 0.10, _ => 0.08 };
            double aLose = pos switch { "F" => -0.06, "M" => -0.04, "D" => -0.08, "G" => -0.05, _ => -0.04 };
            score += aRate >= 0.60 ? aWin : aRate < 0.40 ? aLose : 0;
        }

        return Math.Min(score, 0.4);
    }

    // ── X. Dribbling (cap: +0.3) ─────────────────────────────────────────────
    private static double DribblingScore(PlayerMatchStatistic s, string pos)
    {
        int attempted = s.DribblesAttempted ?? 0;
        int success   = s.DribblesSuccess ?? 0;
        if (attempted == 0) return 0;

        double rate = (double)success / attempted;
        // Fix 6: volume check — avoid farming 1/1
        double volumeMult = attempted < 3 ? 0.5 : 1.0;

        double baseScore = rate >= 0.70 ? 0.2
                         : rate >= 0.50 ? 0.1
                         : rate <  0.40 ? -0.08
                         : 0; // 40–49% = 0

        double score = baseScore * volumeMult;

        // Bonus if DribblesSuccess >= 3 (F and M only)
        if (success >= 3)
            score += pos == "F" ? 0.08 : pos == "M" ? 0.06 : 0;

        return Math.Min(score, 0.3);
    }

    // ── III. Team Result Bonus (cap: +0.3) ───────────────────────────────────
    private static double TeamResultBonus(string? matchResult, double rawBeforeBonus)
    {
        double bonus = matchResult switch
        {
            "W" =>  0.3,
            "L" => -0.2,
            _   =>  0.0  // draw or unknown
        };

        // Fix 6: player played poorly → halve the bonus effect
        if (rawBeforeBonus < 6.0)
            bonus *= 0.5;

        return Math.Clamp(bonus, -0.2, 0.3);
    }

    // ── XI. GK Score (cap: +3.0, G only) ────────────────────────────────────
    private static double GKScore(PlayerMatchStatistic s)
    {
        double score = 0;

        // Fix 7: SavesInsideBox ⊂ Saves — count once with higher weight
        int totalSaves   = s.Saves ?? 0;
        int savesInside  = Math.Min(s.SavesInsideBox ?? 0, totalSaves); // guard
        int savesOutside = totalSaves - savesInside;
        score += Math.Min(savesOutside * 0.35 + savesInside * 0.50, 2.0);

        // Clean sheet (Fix 5: Saves < 2 → reduced bonus)
        if ((s.GoalsConceded ?? 0) == 0)
        {
            if ((s.Minutes ?? 0) >= 60)
                score += (s.Saves ?? 0) >= 2 ? 1.0 : 0.6;
            else if ((s.Minutes ?? 0) >= 30)
                score += 0.5;
        }

        // Goals conceded: -0.4/goal, cap -1.6
        score -= Math.Min((s.GoalsConceded ?? 0) * 0.4, 1.6);

        // Penalties saved: +1.2 each
        score += (s.PenaltiesSaved ?? 0) * 1.2;

        // Runs out successful (cap: +0.30)
        score += Math.Min((s.RunsOutSuccessful ?? 0) * 0.15, 0.30);

        // High claims (cap: +0.20)
        score += Math.Min((s.HighClaims ?? 0) * 0.10, 0.20);

        // Section XIV: GK penalty shootout
        // Cản phá +0.6/lần, thủng lưới -0.1/lần
        if ((s.PenaltyShootoutSaved ?? 0) > 0)
            score += s.PenaltyShootoutSaved!.Value * 0.6;
        if ((s.PenaltyShootoutConceded ?? 0) > 0)
            score -= s.PenaltyShootoutConceded!.Value * 0.1;

        return Math.Min(score, 3.0);
    }

    // ── XII. Negative Score (cap: -2.5) ──────────────────────────────────────
    private static double NegativeScore(PlayerMatchStatistic s)
    {
        double score = 0;
        score += Math.Min((s.FoulsCommitted  ?? 0) * 0.08, 0.48);
        score += Math.Min((s.PenaltiesCommitted ?? 0) * 0.8, 0.8);
        score += Math.Min((s.PossessionLost  ?? 0) * 0.05, 0.75); // Fix 2.3
        score += Math.Min((s.Offsides        ?? 0) * 0.06, 0.18);
        score += Math.Min((s.Dispossessed    ?? 0) * 0.05, 0.25);
        score += Math.Min((s.UnsuccessfulTouch ?? 0) * 0.03, 0.30);
        return Math.Min(score, 2.5);
    }

    // ── XIII. Card Penalty ───────────────────────────────────────────────────
    private static double CardPenalty(PlayerMatchStatistic s, string pos)
    {
        bool twoYellows = (s.YellowCards ?? 0) >= 2;
        bool hasRed     = (s.RedCards    ?? 0) >= 1 && !twoYellows;
        bool oneYellow  = (s.YellowCards ?? 0) == 1 && !twoYellows;

        if (twoYellows)
            return pos switch { "F" => 1.9, "M" => 2.5, "D" => 2.5, "G" => 3.8, _ => 2.5 };
        if (hasRed)
            return pos switch { "F" => 1.5, "M" => 2.0, "D" => 2.0, "G" => 3.0, _ => 2.0 };
        if (oneYellow)
            return pos switch { "F" => 0.4, "M" => 0.5, "D" => 0.5, "G" => 0.8, _ => 0.5 };
        return 0;
    }

    // ── IV. Time Normalization ───────────────────────────────────────────────
    private static double NormalizeByMinutes(double raw, int minutes, PlayerMatchStatistic s)
    {
        if (minutes >= 60) return raw;

        double timeFactor   = minutes / 90.0;
        double impactBonus  = CalcImpactBonus(s, minutes);
        double impactMult   = 1.0 + impactBonus;
        double final        = raw * (0.6 + 0.4 * timeFactor) * impactMult;

        // Fix 1: floor for super sub with impact
        if (impactBonus > 0)
            final = Math.Max(final, 6.2);

        return final;
    }

    // ── V. Impact Bonus (ra sân ≤ 30 phút) ──────────────────────────────────
    private static double CalcImpactBonus(PlayerMatchStatistic s, int minutes)
    {
        double bonus = 0;

        // Ghi bàn quyết định khi ra sân ≤ 20 phút
        if (minutes <= 20)
        {
            if ((s.Goals   ?? 0) > 0) bonus += 0.4;
            if ((s.Assists ?? 0) > 0) bonus += 0.2;
        }

        // Ghi bàn/kiến tạo trong hiệp phụ (+0.2)
        if ((s.IsExtraTime ?? false) &&
            ((s.GoalsInExtraTime ?? 0) > 0 || (s.AssistsInExtraTime ?? 0) > 0))
            bonus += 0.2;

        // Sút penalty thành công trong loạt luân lưu (+0.3)
        if ((s.PenaltyShootoutScored ?? 0) > 0)
            bonus += 0.3;

        // Cản phá penalty trong loạt luân lưu GK (+0.6/lần)
        if ((s.PenaltyShootoutSaved ?? 0) > 0)
            bonus += s.PenaltyShootoutSaved!.Value * 0.6;

        return bonus;
    }

    // ── Helper: determine match result for a player ──────────────────────────
    public static string? GetMatchResult(PlayerMatchStatistic s, Match match)
    {
        if (match.HomeGoals == null || match.AwayGoals == null) return null;
        bool isHome  = s.TeamId == match.HomeTeamId;
        int myGoals  = isHome ? match.HomeGoals.Value : match.AwayGoals.Value;
        int oppGoals = isHome ? match.AwayGoals.Value : match.HomeGoals.Value;
        return myGoals > oppGoals ? "W" : myGoals < oppGoals ? "L" : "D";
    }
}
