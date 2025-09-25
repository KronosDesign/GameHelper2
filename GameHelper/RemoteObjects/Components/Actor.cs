// <copyright file="Actor.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects.Components
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using GameHelper.Utils;
    using GameOffsets.Objects.Components;
    using GameOffsets.Objects.FilesStructures;
    using ImGuiNET;
    using RemoteEnums;

    /// <summary>
    ///     The <see cref="Actor" /> component in the entity.
    /// </summary>
    public class Actor : ComponentBase
    {
        // private Dictionary<IntPtr, VaalSoulStructure> ActiveSkillsVaalSouls { get; } = new();

        private Dictionary<uint, ActiveSkillCooldown> ActiveSkillCooldowns { get; } = new();

        /// <summary>
        ///     Initializes a new instance of the <see cref="Actor" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="Actor" /> component.</param>
        public Actor(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     Gets a value indicating what the player is doing.
        /// </summary>
        public int Animation { get; private set; } = 0x00;

        /// <summary>
        ///     Gets the details of all known Active Skills.
        /// </summary>
        public Dictionary<string, ActiveSkillDetails> ActiveSkills { get; } = new();

        /// <summary>
        ///     Collects all metrics per skill (DPS/APS/AVG/DoT/etc., and Cooldown).
        ///     If present, cooldown is stored under the label "CooldownMs" with the raw millisecond value.
        /// </summary>
        public Dictionary<string, List<(string Label, decimal Value)>> SkillMetrics { get; } = new();

        /// <summary>
        ///     Gets a value indicating if the skill can be used or not.
        /// </summary>
        public HashSet<string> IsSkillUsable { get; } = new();

        /// <summary>
        ///     Gets the total number of entities of a given DeployedObjectType is deployed by this entity.
        /// </summary>
        public int[] DeployedEntities { get; private set; } = new int[256];

        // Raw list (no dedupe) so Actor can show every entry like ActiveSkills component
        private struct SkillRow
        {
            public int Index;
            public IntPtr EntryPtr;
            public ActiveSkillDetails Details;
            public string Name; // empty if no GEPL
            public bool HasGepl;
            public List<(string Label, decimal Value)> Metrics;
        }
        private readonly List<SkillRow> RawSkills = new();

        // Diagnostics for ActiveSkills vector layout (optional, shown in UI)
        private long _asVecBytes;
        private int _asCountGuess8;
        private int _asCountGuess16;
        private bool _asUsed8ByteLayout;

        /// <summary>
        ///     Converts the <see cref="Actor" /> class data to ImGui.
        /// </summary>
        internal override void ToImGui()
        {
            base.ToImGui();
            ImGui.Text($"AnimationId: {this.Animation}");

            if (ImGui.TreeNode("Cooldowns"))
            {
                foreach (var (skillId, skillDetails) in this.ActiveSkillCooldowns)
                {
                    if (ImGui.TreeNode($"{skillId:X}"))
                    {
                        ImGui.Text($"Active Skill Id: {skillDetails.ActiveSkillsDatId}");
                        ImGuiHelper.IntPtrToImGui(
                            $"Cooldowns Vector (Length {skillDetails.TotalActiveCooldowns()})",
                            skillDetails.CooldownsList.First);
                        ImGui.Text($"Max Uses: {skillDetails.MaxUses}");
                        ImGui.Text($"Total Cooldown Time (ms): {skillDetails.TotalCooldownTimeInMs}");
                        ImGui.TreePop();
                    }
                }

                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Active Skills"))
            {
                // Raw view (no dedupe) — show only rows that are named AND have metrics
                {
                    int visibleCount = 0;
                    for (int i = 0; i < RawSkills.Count; i++)
                        if (!string.IsNullOrEmpty(RawSkills[i].Name) &&
                            RawSkills[i].Metrics is { Count: > 0 })
                            visibleCount++;
                    if (ImGui.TreeNode($"Raw entries"))
                    {
                        for (int i = 0; i < RawSkills.Count; i++)
                        {
                            var row = RawSkills[i];
                            // Only show rows that have a name AND at least one metric
                            if (string.IsNullOrEmpty(row.Name) ||
                            row.Metrics is not { Count: > 0 })
                                continue;

                            // Name is guaranteed non-empty here
                            if (ImGui.TreeNode($"{row.Name}##raw{i}"))
                            {
                                ImGui.Text($"Index: {row.Index}");
                                ImGuiHelper.IntPtrToImGui("EntryPtr", row.EntryPtr);
                                ImGuiHelper.IntPtrToImGui("GEPL", row.Details.GrantedEffectsPerLevelDatRow);
                                ImGuiHelper.IntPtrToImGui("ActiveSkillsDatPtr", row.Details.ActiveSkillsDatPtr);
                                ImGuiHelper.IntPtrToImGui("StatSetsPerLevel", row.Details.GrantedEffectStatSetsPerLevelDatRow);
                                ImGuiHelper.IntPtrToImGui("StatsPtr", row.Details.StatsPtr);
                                ImGui.Text($"HasGEPL: {row.HasGepl}");

                                if (row.Metrics is { Count: > 0 })
                                {
                                    foreach (var (mLabel, mValue) in row.Metrics)
                                    {
                                        if (mLabel == "CooldownMs")
                                        {
                                            var ms = (int)mValue;
                                            ImGui.Text($"Cooldown: {ms} ms  ({FormatSeconds(ms / 1000m)}s)");
                                        }
                                        else
                                        {
                                            ImGui.Text($"{mLabel}: {FormatShort(mValue)}");
                                        }
                                    }
                                }

                                ImGui.TreePop();
                            }
                        }

                        ImGui.TreePop();
                    }
                }

                // Grouped (original view)
                if (ImGui.TreeNode("Grouped by name"))
                {
                    foreach (var (skillname, skilldetails) in this.ActiveSkills)
                    {
                        if (ImGui.TreeNode($"{skillname}"))
                        {
                            if (this.SkillMetrics.TryGetValue(skillname, out var metrics) && metrics.Count > 0)
                            {
                                foreach (var (label, val) in metrics)
                                {
                                    if (label == "CooldownMs")
                                    {
                                        var ms = (int)val;
                                        ImGui.Text($"CD:      {ms} ms ({FormatSeconds(ms / 1000m)}s)");
                                    }
                                    else
                                    {
                                        ImGui.Text($"{label}: {FormatShort(val)}");
                                    }
                                }
                            }

                            ImGui.Text($"Use Stage: {skilldetails.UseStage}");
                            ImGui.Text($"Cast Type: {skilldetails.CastType}");
                            ImGui.Text($"Skill UnknownIdAndEquipmentInfo: {skilldetails.UnknownIdAndEquipmentInfo:X}");
                            MiscHelper.ActiveSkillGemDataParser(
                                skilldetails.UnknownIdAndEquipmentInfo,
                                out var iue,
                                out var iu,
                                out var si,
                                out var li,
                                out var inv,
                                out var uid);
                            ImGui.Text($"Can skill be on player item: {iue}");
                            ImGui.Text($"Not sure what this does (something related to vaal skill): {iu}");
                            ImGui.Text($"Skill Gem link Number: {li}");
                            ImGui.Text($"Skill Gem socket Number: {si}");
                            ImGui.Text($"Skill Gem Inventory Slot: {(InventoryName)inv}");
                            ImGui.Text($"Skill Gem Name Hash: {uid:X}");
                            ImGuiHelper.IntPtrToImGui("Granted Effects Per Level Ptr", skilldetails.GrantedEffectsPerLevelDatRow);
                            ImGuiHelper.IntPtrToImGui("Active Skills Ptr", skilldetails.ActiveSkillsDatPtr);
                            ImGuiHelper.IntPtrToImGui("Granted Effect Stat Sets Per Level Ptr", skilldetails.GrantedEffectStatSetsPerLevelDatRow);
                            ImGui.Text($"Total Uses: {skilldetails.TotalUses}");
                            ImGui.Text($"Cooldown Time (ms): {skilldetails.TotalCooldownTimeInMs}");
                            ImGui.TreePop();
                        }
                    }
                    ImGui.TreePop();
                }

                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Can use skills"))
            {
                foreach (var skill in this.IsSkillUsable)
                {
                    ImGui.Text($"Skill {skill} can be used.");
                }

                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Deployed Objects"))
            {
                ImGui.Text("Please throw mines, totem, minions, traps, etc to populate the data over here.");
                for (var i = 0; i < this.DeployedEntities.Length; i++)
                {
                    if (this.DeployedEntities[i] > 0)
                    {
                        ImGui.Text($"Object Type: {i}, Total Count: {this.DeployedEntities[i]}");
                    }
                }

                ImGui.TreePop();
            }
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<ActorOffset>(this.Address);
            this.OwnerEntityAddress = data.Header.EntityPtr;
            this.Animation = data.AnimationId;
            this.IsSkillUsable.Clear();

            // Rebuild cooldowns fresh every tick to avoid stale entries.
            this.ActiveSkillCooldowns.Clear();
            var cooldowns = reader.ReadStdVector<ActiveSkillCooldown>(data.CooldownsPtr);
            for (var i = 0; i < cooldowns.Length; i++)
            {
                this.ActiveSkillCooldowns[cooldowns[i].UnknownIdAndEquipmentInf0] = cooldowns[i];
            }

            this.ActiveSkills.Clear();
            this.SkillMetrics.Clear();
            this.RawSkills.Clear();

            // ActiveSkills vector diagnostics (for UI)
            {
                var first = data.ActiveSkillsPtr.First;
                var last = data.ActiveSkillsPtr.Last;
                _asVecBytes = (long)last - (long)first;
                _asCountGuess8 = (int)(_asVecBytes / IntPtr.Size);
                _asCountGuess16 = (int)(_asVecBytes / (2 * IntPtr.Size));
            }

            // Enumerate active skills: try 16B layout first, fallback to 8B if that yields no named entries.
            int added = 0;
            {
                var entries16 = reader.ReadStdVector<ActiveSkillStructure>(data.ActiveSkillsPtr);
                for (var i = 0; i < entries16.Length; i++)
                {
                    added += this.ParseAndStore(entries16[i].ActiveSkillPtr, i);
                }
                _asUsed8ByteLayout = false;
            }

            if (added == 0)
            {
                var entries8 = reader.ReadStdVector<ActiveSkillPtrOnly>(data.ActiveSkillsPtr);
                for (var i = 0; i < entries8.Length; i++)
                {
                    added += this.ParseAndStore(entries8[i].ActiveSkillPtr, i);
                }
                _asUsed8ByteLayout = true;
            }

            // Deployed entities
            Array.Fill(this.DeployedEntities, 0);
            var deployedEntities = reader.ReadStdVector<DeployedEntityStructure>(data.DeployedEntityArray);
            for (var i = 0; i < deployedEntities.Length; i++)
            {
                if (deployedEntities[i].DeployedObjectType < this.DeployedEntities.Length &&
                    deployedEntities[i].DeployedObjectType >= 0)
                {
                    this.DeployedEntities[deployedEntities[i].DeployedObjectType]++;
                }
            }
        }

        /// <summary>
        ///     Read one entry, always add to RawSkills; if it has GEPL, also update grouped dictionaries.
        ///     Returns 1 if a named skill was added to grouped dictionaries.
        /// </summary>
        private int ParseAndStore(IntPtr entryPtr, int index)
        {
            var reader = Core.Process.Handle;
            var skillDetails = reader.ReadMemory<ActiveSkillDetails>(entryPtr);

            string name = "";
            bool hasGepl = skillDetails.GrantedEffectsPerLevelDatRow != IntPtr.Zero;

            if (hasGepl)
            {
                (name, skillDetails.ActiveSkillsDatPtr) = ((string, IntPtr))Core.GgpkObjectCache
                    .AddOrGetExisting(skillDetails.GrantedEffectsPerLevelDatRow, key =>
                    {
                        return (reader.ReadUnicodeString(
                                    reader.ReadMemory<IntPtr>(reader.ReadMemory<IntPtr>(key))),
                                reader.ReadMemory<GrantedEffectsDatOffset>(
                                    reader.ReadMemory<GrantedEffectsPerLevelDatOffset>(key).GrantedEffectDatPtr)
                                      .ActiveSkillDatPtr);
                    });
            }

            var metrics = CollectSkillMetrics(skillDetails.StatsPtr);

            this.RawSkills.Add(new SkillRow
            {
                Index = index,
                EntryPtr = entryPtr,
                Details = skillDetails,
                Name = name,
                HasGepl = hasGepl,
                Metrics = metrics,
            });

            // Only update grouped dictionaries if we have a GEPL/name
            if (!hasGepl) return 0;

            var cannotbeused = false;
            if (this.ActiveSkillCooldowns.TryGetValue(skillDetails.UnknownIdAndEquipmentInfo, out var cooldownInfo))
                cannotbeused |= cooldownInfo.CannotBeUsed();

            this.ActiveSkills[name] = skillDetails;
            if (!cannotbeused)
                this.IsSkillUsable.Add(name);

            if (metrics.Count > 0)
                this.SkillMetrics[name] = metrics;

            return 1;
        }

        private static string FormatSeconds(decimal sec) => sec.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>
        ///     Collects all relevant metrics from StatsPtr and returns a list of (Label, Value).
        ///     Current set: DPS, APS, AVG, Fire DoT/s, Chaos DoT/s, BaseAVG and Cooldown
        /// </summary>
        private static List<(string Label, decimal Value)> CollectSkillMetrics(IntPtr statsPtr)
        {
            var result = new List<(string, decimal)>(6);
            if (statsPtr == IntPtr.Zero) return result;

            var reader = Core.Process.Handle;
            var sub = reader.ReadMemory<SubStatsComponentOffsets>(statsPtr);
            var pairs = reader.ReadStdVector<StatPair>(sub.Stats);

            int? cooldownMs = null;
            int? dps100 = null;
            int? aps100 = null;
            int? avg100 = null;
            int? dotFirePerMinute = null;
            int? dotChaosPerMinute = null;
            int? baseAvg100 = null;

            static int ID(GameStats s) => (int)s;
            for (int i = 0; i < pairs.Length; i++)
            {
                var id = pairs[i].StatId;
                var v = pairs[i].Value;
                if (id == ID(GameStats.display_skill_cooldown_time_ms)) cooldownMs = v;
                else if (id == ID(GameStats.hundred_times_damage_per_second)) dps100 = v;
                else if (id == ID(GameStats.hundred_times_attacks_per_second)) aps100 = v;
                else if (id == ID(GameStats.hundred_times_average_damage_per_hit)) avg100 = v;
                else if (id == ID(GameStats.intermediary_fire_skill_dot_area_damage_to_deal_per_minute)) dotFirePerMinute = v;
                else if (id == ID(GameStats.intermediary_chaos_skill_dot_damage_to_deal_per_minute)) dotChaosPerMinute = v;
                else if (id == ID(GameStats.base_skill_show_average_damage_instead_of_dps)) baseAvg100 = v;
            }

            if (cooldownMs.HasValue && cooldownMs.Value > 0) result.Add(("CooldownMs", cooldownMs.Value));
            if (dps100.HasValue) result.Add(("DPS", dps100.Value / 100m));
            if (aps100.HasValue) result.Add(("APS", aps100.Value / 100m));
            if (avg100.HasValue) result.Add(("AVG", avg100.Value / 100m));
            if (dotFirePerMinute.HasValue) result.Add(("DoT/s", dotFirePerMinute.Value / 60m));
            if (dotChaosPerMinute.HasValue) result.Add(("Chaos DoT/s", dotChaosPerMinute.Value / 60m));
            if (baseAvg100.HasValue) result.Add(("BaseAVG", baseAvg100.Value / 100m));

            return result;
        }

        private static string FormatShort(decimal v)
        {
            if (v > 999_999_999m) return v.ToString("0,,,.###B", CultureInfo.InvariantCulture);
            if (v > 999_999m) return v.ToString("0,,.##M", CultureInfo.InvariantCulture);
            if (v > 999m) return v.ToString("0,.##K", CultureInfo.InvariantCulture);
            return v.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///     Exposes the raw (non-deduped) list of active skills, filtered to:
        ///     - named rows; AND
        ///     - rows that have at least one metric from CollectSkillMetrics().
        ///     Useful for UIs that want real, measurable entries (still includes duplicates by name).
        /// </summary>
        public IEnumerable<(int Index,
                            string Name,
                            ActiveSkillDetails Details,
                            IReadOnlyList<(string Label, decimal Value)> Metrics)>
            EnumerateRawNamedSkills()
        {
            // Note: RawSkills may contain duplicates by Name; we keep them.
            // Metrics is returned as IReadOnlyList to discourage mutation by consumers.
            foreach (var r in RawSkills)
            {
                if (!string.IsNullOrEmpty(r.Name) &&
                    r.Metrics is { Count: > 0 })
                    yield return (r.Index, r.Name, r.Details, r.Metrics);
            }
        }
    }
}