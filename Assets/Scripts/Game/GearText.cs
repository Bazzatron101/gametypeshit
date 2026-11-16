using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HunterSystem.Simulation;

namespace HunterSystem.Game
{
    /// <summary>
    /// Plain words for what a piece of gear does (ticket 48), for the gear screen: a weapon's light and
    /// heavy attacks, and each worn modifier as one short line ("+15% damage to fire skills").
    /// </summary>
    public static class GearText
    {
        /// <summary>Every line saying what the piece does while worn, attacks first.</summary>
        public static List<string> Effects(GearDefinition gear, Func<string, string> skillName = null)
        {
            var lines = new List<string>();
            if (gear == null) return lines;
            if (gear.LightAttack != null) lines.Add("Light attack: " + Attack(gear.LightAttack));
            if (gear.HeavyAttack != null) lines.Add("Heavy attack: " + Attack(gear.HeavyAttack));
            foreach (var b in gear.StatBonuses) lines.Add(StatBonus(b.Stat, b.Amount));
            foreach (var m in gear.Modifiers) lines.Add(Modifier(m, skillName));
            if (lines.Count == 0) lines.Add("No effect yet.");
            return lines;
        }

        /// <summary>"Slash: 12 damage, 3.2 m reach, 150° wide, every 0.35 s".</summary>
        public static string Attack(WeaponAttack attack)
        {
            var hit = attack.Hit;
            var parts = new List<string> { $"{hit.Damage} damage" };
            switch (hit.Effect)
            {
                case SkillEffectKind.Cone:
                    parts.Add($"{N(hit.Range)} m reach");
                    parts.Add($"{N(hit.ArcDegrees)}° wide");
                    break;
                case SkillEffectKind.Area:
                    parts.Add($"{N(hit.Radius)} m all around");
                    break;
                case SkillEffectKind.Projectile:
                    parts.Add($"{N(hit.Range)} m range");
                    if (hit.Pierces) parts.Add("pierces");
                    break;
            }
            if (attack.WindupSeconds > 0f) parts.Add($"{N(attack.WindupSeconds)} s windup");
            parts.Add($"every {N(hit.CooldownSeconds)} s");
            return $"{hit.Name}: {string.Join(", ", parts)}";
        }

        /// <summary>"+5 Vitality", "-2 Agility". Sense and Luck say they do nothing yet (ticket 51).</summary>
        public static string StatBonus(Stat stat, int amount) =>
            $"{Signed(amount)} {stat}" + (StatDoesNothingYet(stat) ? " (no effect yet)" : "");

        /// <summary>Sense and Luck have no effect in play yet (ticket 06); gear may still carry them.</summary>
        public static bool StatDoesNothingYet(Stat stat) => stat == Stat.Sense || stat == Stat.Luck;

        /// <summary>One modifier as a short line: what it changes, then what it applies to.</summary>
        public static string Modifier(ModifierDefinition m, Func<string, string> skillName = null)
        {
            var changes = new List<string>();
            if (m.DamagePercent != 0) changes.Add($"{Signed(m.DamagePercent)}% damage");
            if (m.CooldownPercent != 0) changes.Add($"{Signed(m.CooldownPercent)}% cooldown");
            if (m.RangeAdd != 0f) changes.Add($"{Signed(m.RangeAdd)} m reach");
            if (m.RadiusAdd != 0f) changes.Add($"{Signed(m.RadiusAdd)} m radius");
            if (m.ExtraProjectiles != 0) changes.Add($"{Signed(m.ExtraProjectiles)} {(Math.Abs(m.ExtraProjectiles) == 1 ? "bolt" : "bolts")}");
            if (m.ExplosionRadius > 0f) changes.Add($"hits burst for {m.ExplosionDamagePercent}% in {N(m.ExplosionRadius)} m");
            var what = changes.Count > 0 ? string.Join(", ", changes) : (string.IsNullOrEmpty(m.Name) ? m.Id : m.Name);

            string target;
            if (!string.IsNullOrEmpty(m.AppliesToSkillId)) target = "to " + (skillName?.Invoke(m.AppliesToSkillId) ?? Pretty(m.AppliesToSkillId));
            else if (!string.IsNullOrEmpty(m.AppliesToTag)) target = $"to {m.AppliesToTag} attacks and skills";
            else target = "";
            var line = target.Length > 0 ? $"{what} {target}" : what;
            if (!string.IsNullOrEmpty(m.PerWornTag)) line += $", for each {m.PerWornTag} piece you wear";
            return line;
        }

        /// <summary>
        /// What changes if <paramref name="candidate"/> replaces <paramref name="worn"/>: for weapons, the
        /// attacks' damage and reach side by side; otherwise what is gained and what is lost.
        /// </summary>
        public static List<string> Compare(GearDefinition candidate, GearDefinition worn, Func<string, string> skillName = null)
        {
            var lines = new List<string>();
            if (candidate == null) return lines;
            if (worn == null)
            {
                // Gains only, unless the piece takes a stat away: then say what it costs.
                if (candidate.StatBonuses.All(b => b.Amount >= 0))
                {
                    lines.Add($"The {GearPanel.SlotName(candidate.Slot)} slot is empty: you only gain.");
                    return lines;
                }
                lines.Add($"The {GearPanel.SlotName(candidate.Slot)} slot is empty.");
                StatChanges(lines, candidate, null);
                return lines;
            }
            if (worn == candidate) return lines;
            AttackChange(lines, "Light", candidate.LightAttack, worn.LightAttack);
            AttackChange(lines, "Heavy", candidate.HeavyAttack, worn.HeavyAttack);
            StatChanges(lines, candidate, worn);
            var gained = candidate.Modifiers.Select(m => Modifier(m, skillName)).ToList();
            var lost = worn.Modifiers.Select(m => Modifier(m, skillName)).ToList();
            foreach (var line in gained.Except(lost)) lines.Add("+ " + line);
            foreach (var line in lost.Except(gained)) lines.Add("− " + line);
            if (lines.Count == 0) lines.Add("No difference.");
            return lines;
        }

        /// <summary>The net change to each stat if <paramref name="candidate"/> replaces <paramref name="worn"/>: "+ 3 Vitality", "− 2 Agility".</summary>
        private static void StatChanges(List<string> lines, GearDefinition candidate, GearDefinition worn)
        {
            foreach (Stat stat in Enum.GetValues(typeof(Stat)))
            {
                var change = candidate.BonusTo(stat) - (worn?.BonusTo(stat) ?? 0);
                if (change == 0) continue;
                var note = StatDoesNothingYet(stat) ? " (no effect yet)" : "";
                lines.Add(change > 0 ? $"+ {change} {stat}{note}" : $"− {-change} {stat}{note}");
            }
        }

        private static void AttackChange(List<string> lines, string kind, WeaponAttack now, WeaponAttack was)
        {
            if (now == null && was == null) return;
            if (now == null) { lines.Add($"{kind}: back to the default attack (from {was.Hit.Name})"); return; }
            if (was == null) { lines.Add($"{kind}: {now.Hit.Name} instead of the default attack"); return; }
            var parts = new List<string> { $"{was.Hit.Damage} → {now.Hit.Damage} damage" };
            var nowReach = now.Hit.Effect == SkillEffectKind.Area ? now.Hit.Radius : now.Hit.Range;
            var wasReach = was.Hit.Effect == SkillEffectKind.Area ? was.Hit.Radius : was.Hit.Range;
            if (!Mathf.Approximately(nowReach, wasReach)) parts.Add($"{N(wasReach)} → {N(nowReach)} m reach");
            if (!Mathf.Approximately(now.Hit.CooldownSeconds, was.Hit.CooldownSeconds))
                parts.Add($"every {N(was.Hit.CooldownSeconds)} → {N(now.Hit.CooldownSeconds)} s");
            lines.Add($"{kind}: {was.Hit.Name} → {now.Hit.Name} ({string.Join(", ", parts)})");
        }

        private static string N(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString(CultureInfo.InvariantCulture);
        private static string Signed(float value) => (value > 0 ? "+" : "") + N(value);

        /// <summary>"fire-bolt" → "Fire Bolt".</summary>
        private static string Pretty(string id) =>
            string.Join(" ", id.Split('-', '_').Where(w => w.Length > 0).Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));

        private static class Mathf
        {
            public static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.001f;
        }
    }
}
