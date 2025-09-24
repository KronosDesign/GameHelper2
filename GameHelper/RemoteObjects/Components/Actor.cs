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
        ///     Gets parsed Stats for each known active skill, keyed by skill name.
        /// </summary>
        public Dictionary<string, Dictionary<GameStats, int>> SkillStats { get; } = new();

        /// <summary> 
        ///     Get stats metrics of all known active Skills. 
        /// </summary>
        public Dictionary<string, (decimal APS, decimal DPS, decimal AVG, decimal DOTps, decimal AvgDPS, int CDms)> SkillMetrics { get; } = new();

        /// <summary>
        ///     Gets a value indicating if the skill can be used or not.
        /// </summary>
        public HashSet<string> IsSkillUsable { get; } = new();

        /// <summary>
        ///     Gets the total number of entities of a given DeployedObjectType is deployed by this entity.
        /// </summary>
        public int[] DeployedEntities { get; private set; } = new int[256];

        /// <summary>
        ///     Converts the <see cref="Actor" /> class data to ImGui.
        /// </summary>
        internal override void ToImGui()
        {
            base.ToImGui();
            ImGui.Text($"AnimationId: {this.Animation}");
            // if (ImGui.TreeNode("Vaal Souls"))
            // {
            //     foreach(var (skillNamePtr, skillDetails) in this.ActiveSkillsVaalSouls)
            //     {
            //         if (ImGui.TreeNode($"{skillNamePtr.ToInt64():X}"))
            //         {
            //             ImGui.Text($"Required Souls: {skillDetails.RequiredSouls}");
            //             ImGui.Text($"Current Souls: {skillDetails.CurrentSouls}");
            //             ImGui.TreePop();
            //         }
            //     }
            //
            //     ImGui.TreePop();
            // }

            if (ImGui.TreeNode("Cooldowns"))
            {
                foreach(var (skillId, skillDetails) in this.ActiveSkillCooldowns)
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
                foreach (var (skillname, skilldetails) in this.ActiveSkills)
                {
                    if (ImGui.TreeNode($"{skillname}"))
                    {
                        if (this.SkillMetrics.TryGetValue(skillname, out var m))
                        {
                            if (m.APS > 0) ImGui.Text($"APS:     {FormatAps(m.APS)}");
                            if (m.DPS > 0) ImGui.Text($"DPS:     {FormatValue(m.DPS)}");
                            if (m.AVG > 0) ImGui.Text($"AvgHit:  {FormatValue(m.AVG)}");
                            if (m.DOTps > 0) ImGui.Text($"DoT:     {FormatValue(m.DOTps)}/s");
                            if (m.AvgDPS > 0) ImGui.Text($"AvgDPS:  {FormatValue(m.AvgDPS)}");
                            if (m.CDms > 0) ImGui.Text($"CD:      {m.CDms} ms ({FormatSeconds(m.CDms / 1000m)}s)");
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
                        ImGuiHelper.IntPtrToImGui($"Active Skills Ptr", skilldetails.ActiveSkillsDatPtr);
                        ImGuiHelper.IntPtrToImGui("Granted Effect Stat Sets Per Level Ptr", skilldetails.GrantedEffectStatSetsPerLevelDatRow);
                        //ImGui.Text($"Can be used with weapons: {skilldetails.CanBeUsedWithWeapon}");
                        //ImGui.Text($"Can not be used: {skilldetails.CannotBeUsed}");
                        //ImGui.Text($"Current Vaal Soul (-1 if not vaal skill): {skilldetails.CurrentVaalSouls}");
                        //ImGui.Text($"Unknown0: {skilldetails.UnknownByte0}");
                        //ImGui.Text($"Unknown1: {skilldetails.UnknownByte1}");
                        ImGui.Text($"Total Uses: {skilldetails.TotalUses}");
                        ImGui.Text($"Cooldown Time (ms): {skilldetails.TotalCooldownTimeInMs}");
                        //ImGui.Text($"Souls per use: {skilldetails.SoulsPerUse}");
                        //ImGui.Text($"Total Vaal Uses: {skilldetails.TotalVaalUses}");
                        ImGui.TreePop();
                    }
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
                ImGui.Text("Please throw mines, totem, minons, traps, etc to populate the data over here.");
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
            // var skillsvaalsouls = reader.ReadStdVector<VaalSoulStructure>(data.VaalSoulsPtr);
            // for (var i = 0; i < skillsvaalsouls.Length; i++)
            // {
            //     this.ActiveSkillsVaalSouls[skillsvaalsouls[i].ActiveSkillsDatPtr] = skillsvaalsouls[i];
            // }

            var cooldowns = reader.ReadStdVector<ActiveSkillCooldown>(data.CooldownsPtr);
            for (var i = 0; i < cooldowns.Length; i++)
            {
                this.ActiveSkillCooldowns[cooldowns[i].UnknownIdAndEquipmentInf0] = cooldowns[i];
            }

            this.ActiveSkills.Clear();
            this.SkillStats.Clear();
            this.SkillMetrics.Clear();

            var activeSkills = reader.ReadStdVector<ActiveSkillStructure>(data.ActiveSkillsPtr);
            for (var i = 0; i < activeSkills.Length; i++)
            {
                var skillDetails = reader.ReadMemory<ActiveSkillDetails>(activeSkills[i].ActiveSkillPtr);
                if (skillDetails.GrantedEffectsPerLevelDatRow == IntPtr.Zero)
                {
                    // No usecase for these skills.
                    // this.ActiveSkills[i.ToString()] = skillDetails;
                }
                else
                {
                    (var name, skillDetails.ActiveSkillsDatPtr) = ((string, IntPtr))Core.GgpkObjectCache.
                        AddOrGetExisting(skillDetails.GrantedEffectsPerLevelDatRow, (key) =>
                        {
                            return (reader.ReadUnicodeString(
                                reader.ReadMemory<IntPtr>(reader.ReadMemory<IntPtr>(key))),
                                reader.ReadMemory<GrantedEffectsDatOffset>(
                                    reader.ReadMemory<GrantedEffectsPerLevelDatOffset>(
                                        key).GrantedEffectDatPtr).ActiveSkillDatPtr);
                        });

                    // skillDetails.CurrentVaalSouls = -1;
                    var cannotbeused = false;
                    if (this.ActiveSkillCooldowns.TryGetValue(skillDetails.UnknownIdAndEquipmentInfo, out var cooldownInfo))
                    {
                        cannotbeused |= cooldownInfo.CannotBeUsed();
                    }
                    // else if (this.ActiveSkillsVaalSouls.TryGetValue(skillDetails.ActiveSkillsDatPtr, out var vaalSoulInfo))
                    // {
                    //     skillDetails.CurrentVaalSouls = vaalSoulInfo.CurrentSouls;
                    //     cannotbeused |= vaalSoulInfo.CannotBeUsed();
                    // }

                    this.ActiveSkills[name] = skillDetails;
                    if (cannotbeused)
                    {
                    }
                    else
                    {
                        this.IsSkillUsable.Add(name);
                    }

                    var stats = ReadSkillStats(skillDetails.StatsPtr);
                    if (stats.Count > 0)
                        this.SkillStats[name] = stats;

                    var metrics = ComputeMetrics(stats);
                    if (metrics.APS > 0 || metrics.DPS > 0 || metrics.AVG > 0 || metrics.DOTps > 0 || metrics.AvgDPS > 0 || metrics.CDms > 0)
                        this.SkillMetrics[name] = metrics;
                }
            }

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

        private static Dictionary<GameStats, int> ReadSkillStats(IntPtr statsComponentPtr)
        {
            var dict = new Dictionary<GameStats, int>();
            if (statsComponentPtr == IntPtr.Zero)
                return dict;

            var reader = Core.Process.Handle;
            var sub = reader.ReadMemory<SubStatsComponentOffsets>(statsComponentPtr);
            var pairs = reader.ReadStdVector<StatPair>(sub.Stats);

            for (int i = 0; i < pairs.Length; i++)
            {
                var id = pairs[i].StatId;
                var val = pairs[i].Value;
                if (Enum.IsDefined(typeof(GameStats), id))
                    dict[(GameStats)id] = val;
            }
            return dict;
        }

        private static string FormatValue(decimal value)
        {
            if (value > 999_999_999m) return value.ToString("0,,,.###B", CultureInfo.InvariantCulture);
            if (value > 999_999m) return value.ToString("0,,.##M", CultureInfo.InvariantCulture);
            if (value > 999m) return value.ToString("0,.##K", CultureInfo.InvariantCulture);
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string FormatAps(decimal aps) => aps.ToString("0.##", CultureInfo.InvariantCulture);
        private static string FormatSeconds(decimal sec) => sec.ToString("0.###", CultureInfo.InvariantCulture);

        private static (decimal APS, decimal DPS, decimal AVG, decimal DOTps, decimal AvgDPS, int CDms)
            ComputeMetrics(Dictionary<GameStats, int> stats)
        {
            if (stats == null || stats.Count == 0) return (0, 0, 0, 0, 0, 0);

            decimal aps = 0, statDps = 0, avgHit = 0, dotps = 0;
            int cdms = 0;

            if (stats.TryGetValue(GameStats.hundred_times_attacks_per_second, out var aps100))
                aps = aps100 / 100m;

            if (stats.TryGetValue(GameStats.hundred_times_damage_per_second, out var dps100))
                statDps = dps100 / 100m;

            if (stats.TryGetValue(GameStats.hundred_times_average_damage_per_skill_use, out var avg100))
                avgHit = avg100 / 100m;

            if (stats.TryGetValue(GameStats.base_skill_show_average_damage_instead_of_dps, out var baseAvg100))
                if (baseAvg100 > 0) avgHit = Math.Max(avgHit, baseAvg100 / 100m);

            if (stats.TryGetValue(GameStats.intermediary_fire_skill_dot_damage_to_deal_per_minute, out var dotPerMin))
                dotps = dotPerMin / 60m;

            if (stats.TryGetValue(GameStats.display_skill_cooldown_time_ms, out var cd))
                cdms = cd;

            var avgDps = (aps > 0 && avgHit > 0) ? aps * avgHit : 0;

            return (aps, statDps, avgHit, dotps, avgDps, cdms);
        }
    }
}