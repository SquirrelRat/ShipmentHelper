using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.Elements.Village;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ImGuiNET;
using SharpDX;
using ExileCore.Shared.Attributes;
using ExileCore.PoEMemory.Components;

namespace ShipmentHelper
{
    public class ShipmentHelper : BaseSettingsPlugin<ShipmentHelperSettings>
    {
        public enum StrategyType { Recipe, Automatic, Quota }
        public class PortQuota
        {
            public string PortName { get; set; }
            public string ResourceName { get; set; }
            public int DeliveredAmount { get; set; }
            public int RequestedAmount { get; set; }
            public bool IsCompleted => DeliveredAmount >= RequestedAmount;
        }

        [Submenu]
        public class ShippingStrategy
        {
            public string Name { get; set; } = "New Strategy";
            public StrategyType Type { get; set; } = StrategyType.Recipe;
            public string TargetReward { get; set; } = "Currency";
            public string TargetPort { get; set; } = "Riben Fell";
            public Dictionary<string, int> RequiredResources { get; set; } = new Dictionary<string, int>();
        }

        private readonly List<ShippingStrategy> _defaultStrategies = new List<ShippingStrategy>
        {
            new ShippingStrategy { Name = "[Automatic] Currency", Type = StrategyType.Automatic, TargetReward = "Currency" },
            new ShippingStrategy { Name = "[Automatic] Scarabs", Type = StrategyType.Automatic, TargetReward = "Scarabs" },
            new ShippingStrategy { Name = "[Automatic] Expedition", Type = StrategyType.Automatic, TargetReward = "Expedition" },
            new ShippingStrategy { Name = "[Automatic] Blight", Type = StrategyType.Automatic, TargetReward = "Blight" },
            new ShippingStrategy { Name = "[Automatic] Heist", Type = StrategyType.Automatic, TargetReward = "Heist" },
            new ShippingStrategy { Name = "[Automatic] Essences", Type = StrategyType.Automatic, TargetReward = "Essences" },
            new ShippingStrategy { Name = "[Low-Invest] Tattoos", Type = StrategyType.Recipe, TargetPort = "Ngakanu", RequiredResources = new Dictionary<string, int> { { "Corn", 10000 }, { "Wheat", 10000 }, { "Pumpkin", 10000 }, { "Orgourd", 10000 }, { "Blue Zanthimum", 10000 }, { "Thaumaturgic Dust", 1 } } },
            new ShippingStrategy { Name = "[Low-Invest] Divine Orb", Type = StrategyType.Recipe, TargetPort = "Kalguur", RequiredResources = new Dictionary<string, int> { { "Blue Zanthimum", 10000 } } },
            new ShippingStrategy { Name = "[High-Invest] Mirror Shard", Type = StrategyType.Recipe, TargetPort = "Riben Fell", RequiredResources = new Dictionary<string, int> { { "Corn", 315000 }, { "Wheat", 315000 }, { "Thaumaturgic Dust", 1 } } },
        };

        private static readonly Dictionary<int, string> ResourceMap = new Dictionary<int, string> { { 5, "Crimson Iron Bar" }, { 6, "Orichalcum Bar" }, { 7, "Petrified Amber Bar" }, { 8, "Bismuth Bar" }, { 9, "Verisium Bar" }, { 10, "Wheat" }, { 11, "Corn" }, { 12, "Pumpkin" }, { 13, "Orgourd" }, { 14, "Blue Zanthimum" }, { 15, "Thaumaturgic Dust" } };
        private static readonly Dictionary<int, string> PortIndexMap = new Dictionary<int, string> { { 0, "Riben Fell" }, { 1, "Ngakanu" }, { 2, "Pondium" }, { 3, "Te Onui" }, { 4, "Kalguur" } };
        private bool _isPreparingShipment = false;
        
        private readonly Dictionary<string, (string Port, int Value, string RewardType)> _resourceInfo = new Dictionary<string, (string, int, string)>
        {
            {"Crimson Iron Bar", ("Pondium", 40, "Heist") }, {"Orichalcum Bar", ("Pondium", 40, "Heist") }, {"Petrified Amber Bar", ("Pondium", 50, "Heist") }, {"Bismuth Bar", ("Pondium", 50, "Heist") },
            {"Verisium Bar", ("Riben Fell", 60, "Scarabs") },
            {"Wheat", ("Kalguur", 20, "Currency") }, {"Corn", ("Kalguur", 20, "Currency") },
            {"Pumpkin", ("Te Onui", 20, "Blight") },
            {"Orgourd", ("Riben Fell", 20, "Essences") },
            {"Blue Zanthimum", ("Pondium", 20, "Expedition") }
        };

        public override bool Initialise()
        {
            Settings.AddNewStrategy.OnPressed += () => { Settings.CustomStrategies.Add(new ShippingStrategy()); UpdateAllStrategies(); };
            UpdateAllStrategies();
            return true;
        }

        private void UpdateAllStrategies()
        {
            Settings.AllStrategies.Clear();
            Settings.AllStrategies.Add(new ShippingStrategy { Name = "Prioritize Port Quotas", Type = StrategyType.Quota, TargetPort = "Dynamic" });
            Settings.AllStrategies.AddRange(_defaultStrategies);
            Settings.AllStrategies.AddRange(Settings.CustomStrategies);
            Settings.TargetStrategy.Values = Settings.AllStrategies.Select(s => s.Name).ToList();
            if (string.IsNullOrEmpty(Settings.TargetStrategy.Value) || !Settings.TargetStrategy.Values.Contains(Settings.TargetStrategy.Value))
            {
                Settings.TargetStrategy.Value = Settings.TargetStrategy.Values.FirstOrDefault();
            }
        }

        public override void DrawSettings() { base.DrawSettings(); }

        public override void Render()
        {
            if (!Settings.Enable || _isPreparingShipment) return;
            if (Settings.ShowShipmentTimers.Value) { RenderTimers(); }
            var shipmentConfig = GameController.Game.IngameState.IngameUi.VillageShipmentScreen?.ShipmentConfigurationElement;
            
            ShippingStrategy selectedStrategy = null;
            if (shipmentConfig?.IsVisible == true || Settings.ShowHelperHud.Value)
            {
                selectedStrategy = Settings.AllStrategies.FirstOrDefault(s => s.Name == Settings.TargetStrategy.Value);
            }

            if (Settings.ShowHelperHud.Value && shipmentConfig?.IsVisible == true) { RenderShipmentHud(shipmentConfig, selectedStrategy); }
            if (shipmentConfig?.IsVisible != true) return;
            RenderShipmentOverlay(shipmentConfig, selectedStrategy);
        }

        private List<(string Destination, TimeSpan TimeLeft)> GetActiveShipments()
        {
            var activeShipments = new List<(string, TimeSpan)>();
            var villageScreen = GameController.Game.IngameState.IngameUi.VillageScreen;
            if (villageScreen == null) return activeShipments;
            var times = villageScreen.RemainingShipmentTimes;
            var shipInfos = villageScreen.ShipInfo;
            if (times == null || shipInfos == null) return activeShipments;
            for (int i = 0; i < times.Count; i++)
            {
                if (i >= shipInfos.Count) break;
                var destination = shipInfos[i].TargetPort?.Name;
                if (!string.IsNullOrEmpty(destination))
                {
                    var time = times[i];
                    activeShipments.Add((destination, time));
                }
            }
            return activeShipments;
        }

        private void RenderTimers()
        {
            var activeShipments = GetActiveShipments();
            if (!activeShipments.Any()) return;
            
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(GameController.Window.GetWindowRectangle().Width * Settings.TimerPosX.Value, GameController.Window.GetWindowRectangle().Height * Settings.TimerPosY.Value), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowBgAlpha(0.8f);

            if (ImGui.Begin("Shipment Timers", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar))
            {
                foreach (var shipment in activeShipments)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), shipment.Destination + ":");
                    ImGui.SameLine();

                    if (shipment.TimeLeft.TotalSeconds > 0)
                    {
                        string timeFormat = shipment.TimeLeft.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss";
                        ImGui.Text(shipment.TimeLeft.ToString(timeFormat));
                    }
                    else
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "Ready");
                    }
                }
            }
            ImGui.End();
        }
        
        private void RenderShipmentHud(Element shipmentConfig, ShippingStrategy strategy)
        {
            if (strategy == null) return;
            
            var resourceListContainer = shipmentConfig.GetChildAtIndex(0)?.GetChildAtIndex(2);
            if (resourceListContainer == null) return;
            
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(GameController.Window.GetWindowRectangle().Width * Settings.HudSavedPosX.Value, GameController.Window.GetWindowRectangle().Height * Settings.HudSavedPosY.Value), ImGuiCond.FirstUseEver);
            
            if (ImGui.Begin("Shipment Helper HUD", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.PushItemWidth(172.5f);
                var strategyNames = Settings.AllStrategies.Select(s => s.Name).ToArray();
                var currentStrategyIndex = Array.IndexOf(strategyNames, strategy.Name);
                if (ImGui.Combo("Strategy", ref currentStrategyIndex, strategyNames, strategyNames.Length))
                {
                    Settings.TargetStrategy.Value = strategyNames[currentStrategyIndex];
                    
                    strategy = Settings.AllStrategies.FirstOrDefault(s => s.Name == Settings.TargetStrategy.Value);
                    if (strategy == null) { ImGui.End(); return; }
                }

                if (strategy.Type == StrategyType.Automatic)
                {
                    ImGui.SameLine();
                    var percentageOptions = Settings.AutomaticPercentageToSend.Values.ToArray();
                    var currentPercentageIndex = Settings.AutomaticPercentageToSend.Values.IndexOf(Settings.AutomaticPercentageToSend.Value);
                    if (ImGui.Combo("##percentage", ref currentPercentageIndex, percentageOptions, percentageOptions.Length))
                    {
                        Settings.AutomaticPercentageToSend.Value = percentageOptions[currentPercentageIndex];
                    }
                    ImGui.PopItemWidth();
                    ImGui.Text("Percentage of available resources to send.");
                }
                else
                {
                    ImGui.PopItemWidth();
                }
                ImGui.Separator();

                if (strategy.Type == StrategyType.Quota)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "Showing suggestions for Port Quotas.");
                }
                else
                {
                    var availableResources = GetAvailableResources(resourceListContainer);
                    var addedResources = GetAddedResources(resourceListContainer);
                    var green = new System.Numerics.Vector4(0, 1, 0, 1);
                    var red = new System.Numerics.Vector4(1, 0, 0, 1);
                    var yellow = new System.Numerics.Vector4(1, 1, 0, 1);

                    var goals = GetSuggestedGoals(shipmentConfig, strategy);
                    if(goals != null && goals.Any())
                    {
                        foreach(var goal in goals)
                        {
                            if (goal.Key == "Thaumaturgic Dust") continue;
                            int current = availableResources.GetValueOrDefault(goal.Key, 0);
                            int required = goal.Value;
                            bool hasEnough = current >= required;
                            var lineColor = hasEnough ? green : red;

                            ImGui.TextColored(lineColor, $"[{(hasEnough ? "*" : "x")}]");
                            ImGui.SameLine();
                            ImGui.TextColored(lineColor, $"{goal.Key}: {current} / {required}");
                        }
                    } else {
                        ImGui.Text("No viable shipment for this strategy.");
                    }

                    ImGui.Separator();
                    
                    bool isPrepared = goals?.All(req => req.Key == "Thaumaturgic Dust" || addedResources.GetValueOrDefault(req.Key, 0) >= req.Value) ?? false;
                    string statusText = isPrepared ? "SHIPMENT READY" : "PREPARING...";
                    System.Numerics.Vector4 statusColor = isPrepared ? green : yellow;
                    ImGui.Text("Status: "); ImGui.SameLine();
                    ImGui.TextColored(statusColor, statusText);
                }
                
                ImGui.Separator();
                var suggestedGoalsForButton = GetSuggestedGoals(shipmentConfig, strategy);
                if (suggestedGoalsForButton != null && suggestedGoalsForButton.Any())
                {
                    if (_isPreparingShipment) { ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "PREPARING..."); }
                    else if (ImGui.Button("Prepare Shipment##HUD"))
                    {
                        _ = PrepareShipment(resourceListContainer, suggestedGoalsForButton);
                    }
                }
            }
            ImGui.End();
        }

        private Dictionary<string, int> GetSuggestedGoals(Element shipmentConfig, ShippingStrategy selectedStrategy)
        {
            var resourceListContainer = shipmentConfig?.GetChildAtIndex(0)?.GetChildAtIndex(2);
            if (resourceListContainer == null) return null;
            var availableResources = GetAvailableResources(resourceListContainer);
            Dictionary<string, int> suggestedGoals = null;
            if (selectedStrategy == null) return null;

            if (Settings.DebugLogging.Value) LogMessage($"[Dust Debug] Strategy: {selectedStrategy.Name}", 1);

            switch (selectedStrategy.Type)
            {
                case StrategyType.Quota:
                    var allPortQuotas = GetCurrentPortQuotas();
                    var incompleteQuota = allPortQuotas.FirstOrDefault(q => !q.IsCompleted);
                    if (incompleteQuota != null) { suggestedGoals = GetQuotaSuggestion(incompleteQuota); }
                    break;
                case StrategyType.Recipe:
                    suggestedGoals = GetRecipeSuggestion(availableResources, selectedStrategy);
                    break;
                case StrategyType.Automatic:
                    suggestedGoals = GetAutomaticSuggestion(availableResources, selectedStrategy);
                    break;
            }

            if (suggestedGoals != null && suggestedGoals.Any())
            {
                var addedResources = GetAddedResources(resourceListContainer);
                bool allOtherGoalsMet = suggestedGoals.Where(g => g.Key != "Thaumaturgic Dust").All(g => addedResources.GetValueOrDefault(g.Key, 0) >= g.Value);
                bool shouldSuggestDust = Settings.AlwaysSuggestDust.Value || suggestedGoals.ContainsKey("Thaumaturgic Dust");

                if (Settings.DebugLogging.Value)
                {
                    LogMessage($"[Dust Debug] GoalsMet: {allOtherGoalsMet}, ShouldSuggest: {shouldSuggestDust}", 1);
                    LogMessage($"[Dust Debug] Initial Goals: {string.Join(", ", suggestedGoals.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}", 1);
                }

                if (shouldSuggestDust)
                {
                    int dustAdded = addedResources.GetValueOrDefault("Thaumaturgic Dust", 0);
                    int dustAvailable = availableResources.GetValueOrDefault("Thaumaturgic Dust", 0);
                    int smartDustGoal = 0;

                    var shipmentValueElement = shipmentConfig.GetChildAtIndex(0)?.GetChildAtIndex(1)?.GetChildAtIndex(1);
                    int totalShipmentValue = 0;
                    bool totalShipmentValueParsed = false;

                    if (shipmentValueElement != null && int.TryParse(shipmentValueElement.Text.Replace(",", ""), out totalShipmentValue))
                    {
                        totalShipmentValueParsed = true;
                    }

                    if (Settings.DebugLogging.Value)
                    {
                        LogMessage($"[Dust Debug] AlwaysSuggest: {Settings.AlwaysSuggestDust.Value}, HasInGoals: {suggestedGoals.ContainsKey("Thaumaturgic Dust")}", 1);
                        LogMessage($"[Dust Debug] Value: {totalShipmentValue} (Parsed: {totalShipmentValueParsed})", 1);
                        LogMessage($"[Dust Debug] Added: {dustAdded}, Available: {dustAvailable}", 1);
                    }

                    if (totalShipmentValueParsed && totalShipmentValue > 0)
                    {
                        int idealDustAmount = totalShipmentValue - dustAdded;
                        smartDustGoal = Math.Min(idealDustAmount, dustAvailable);
                        if (Settings.DebugLogging.Value) LogMessage($"[Dust Debug] Smart Calc: ideal={idealDustAmount}, smart={smartDustGoal}", 1);
                    }
                    else if (Settings.AlwaysSuggestDust.Value && dustAvailable > 0)
                    {
                        smartDustGoal = Math.Min(1, dustAvailable);
                        if (Settings.DebugLogging.Value) LogMessage($"[Dust Debug] Fallback Calc: smart={smartDustGoal}", 1);
                    }

                    if (smartDustGoal > 0)
                    {
                        if (!suggestedGoals.ContainsKey("Thaumaturgic Dust")) suggestedGoals.Add("Thaumaturgic Dust", smartDustGoal);
                        else suggestedGoals["Thaumaturgic Dust"] = smartDustGoal;
                        if (Settings.DebugLogging.Value) LogMessage($"[Dust Debug] Suggested: {smartDustGoal}", 1);
                    }
                    else if (suggestedGoals.ContainsKey("Thaumaturgic Dust"))
                    {
                        suggestedGoals.Remove("Thaumaturgic Dust");
                        if (Settings.DebugLogging.Value) LogMessage($"[Dust Debug] Removed.", 1);
                    }
                }
                else if (suggestedGoals.ContainsKey("Thaumaturgic Dust")) { suggestedGoals.Remove("Thaumaturgic Dust"); }
            }
            return suggestedGoals;
        }

        private void RenderShipmentOverlay(Element shipmentConfig, ShippingStrategy strategy)
        {
            var suggestedGoals = GetSuggestedGoals(shipmentConfig, strategy);
            if (suggestedGoals == null) return;
            var resourceListContainer = shipmentConfig.GetChildAtIndex(0)?.GetChildAtIndex(2);
            var addedResources = GetAddedResources(resourceListContainer);
            DrawResourceSuggestions(resourceListContainer, suggestedGoals, addedResources);
            var targetPortName = strategy?.TargetPort;
            if (strategy?.Type == StrategyType.Automatic && suggestedGoals.Any())
            {
                var firstGoalResource = suggestedGoals.First().Key;
                targetPortName = _resourceInfo.FirstOrDefault(x => x.Key == firstGoalResource).Value.Port;
            }
            else if (targetPortName == "Dynamic")
            {
                 var allPortQuotas = GetCurrentPortQuotas();
                 targetPortName = allPortQuotas.FirstOrDefault(q => !q.IsCompleted)?.PortName;
            }
            if (targetPortName != null) { DrawPortSuggestion(GameController.Game.IngameState.IngameUi.VillageShipmentScreen, targetPortName, strategy); }
        }

        private async Task PrepareShipment(Element resourceListContainer, Dictionary<string, int> goals)
        {
            if (_isPreparingShipment || resourceListContainer == null) return;
            _isPreparingShipment = true;
            LogMessage("--- Auto-Shipment Started ---", 5, Color.LawnGreen);
            var invertedResourceMap = ResourceMap.ToDictionary(kp => kp.Value, kp => kp.Key);
            foreach (var goal in goals)
            {
                if (goal.Value <= 0) continue;
                string resourceName = goal.Key;
                int requiredAmount = goal.Value;
                if (!invertedResourceMap.ContainsKey(resourceName)) continue;
                int resourceIndex = invertedResourceMap[resourceName];
                var inputElement = resourceListContainer.GetChildAtIndex(resourceIndex)?.GetChildAtIndex(2)?.GetChildAtIndex(0);
                if (inputElement == null) { LogError($"Could not find input element for {resourceName}", 5); continue; }
                Input.SetCursorPos(inputElement.GetClientRect().Center);
                await Task.Delay(50);
                Input.Click(MouseButtons.Left);
                await Task.Delay(50);
                for (int i = 0; i < 10; i++) { Input.KeyDown(Keys.Back); await Task.Delay(20); Input.KeyUp(Keys.Back); await Task.Delay(20); }
                await Task.Delay(50);
                string amountString = requiredAmount.ToString();
                foreach (char digit in amountString)
                {
                    if (Enum.TryParse($"D{digit}", out Keys key))
                    {
                        Input.KeyDown(key); await Task.Delay(40); Input.KeyUp(key); await Task.Delay(40);
                    }
                }
                await Task.Delay(50);
            }
            LogMessage("--- Auto-Shipment Finished ---", 5, Color.LawnGreen);
            _isPreparingShipment = false;
        }

        private Dictionary<string, int> GetQuotaSuggestion(PortQuota quota)
        {
            string resourceToShip = quota.ResourceName.Replace("Ore", "Bar").Trim();
            int amountNeeded = quota.RequestedAmount - quota.DeliveredAmount;
            return new Dictionary<string, int> { { resourceToShip, amountNeeded } };
        }

        private Dictionary<string, int> GetRecipeSuggestion(Dictionary<string, int> available, ShippingStrategy strategy)
        {
            bool canComplete = strategy.RequiredResources.All(req =>
                req.Key == "Thaumaturgic Dust" || (available.GetValueOrDefault(req.Key, 0) >= req.Value));
            if (canComplete) return new Dictionary<string, int>(strategy.RequiredResources);
            return null;
        }
        
        private Dictionary<string, int> GetAutomaticSuggestion(Dictionary<string, int> available, ShippingStrategy strategy)
        {
            var suggestedGoals = new Dictionary<string, int>();
            var relevantResources = _resourceInfo
                .Where(x => x.Value.RewardType == strategy.TargetReward)
                .Select(x => x.Key)
                .ToList();
            if (!relevantResources.Any()) return null;
            float percentage = 0.75f;
            switch(Settings.AutomaticPercentageToSend.Value)
            {
                case "25%": percentage = 0.25f; break;
                case "50%": percentage = 0.50f; break;
                case "75%": percentage = 0.75f; break;
                case "100%": percentage = 1.00f; break;
            }
            foreach(var resource in relevantResources)
            {
                if (available.TryGetValue(resource, out int availableAmount) && availableAmount > 0)
                {
                    int amountToSend = (int)(availableAmount * percentage);
                    if (amountToSend > 0)
                    {
                        suggestedGoals[resource] = amountToSend;
                    }
                }
            }
            if (suggestedGoals.Any())
            {
                suggestedGoals["Thaumaturgic Dust"] = 1;
                return suggestedGoals;
            }
            return null;
        }

        public class PortInfo
        {
            public string Name { get; set; }
            public int Distance { get; set; }
            public List<string> SpecialRewards { get; set; }
        }

        private readonly List<PortInfo> _portInfos = new List<PortInfo>
        {
            new PortInfo { Name = "Riben Fell", Distance = 140, SpecialRewards = new List<string> { "Scarabs", "Essences", "Currency" } },
            new PortInfo { Name = "Ngakanu", Distance = 186, SpecialRewards = new List<string> { "Tattoos", "Currency" } },
            new PortInfo { Name = "Pondium", Distance = 870, SpecialRewards = new List<string> { "Heist", "Expedition", "Currency" } },
            new PortInfo { Name = "Te Onui", Distance = 939, SpecialRewards = new List<string> { "Blight", "Tattoos", "Currency" } },
            new PortInfo { Name = "Kalguur", Distance = 2317, SpecialRewards = new List<string> { "Currency" } }
        };

        private readonly Dictionary<long, string> _portAddressToNameCache = new Dictionary<long, string>();
        private void DrawPortSuggestion(VillageShipmentScreen villageShipmentScreen, string targetPortName, ShippingStrategy strategy)
        {
            var portMapContainer = villageShipmentScreen.GetChildAtIndex(2)?.GetChildAtIndex(1);
            if (portMapContainer == null) return;

            if (strategy == null) return;

            string targetReward = null;
            if (strategy.Type == StrategyType.Automatic)
            {
                targetReward = strategy.TargetReward;
            }
            else if (strategy.Type == StrategyType.Recipe)
            {
                var portInfo = _portInfos.FirstOrDefault(p => p.Name == strategy.TargetPort);
                if (portInfo != null) targetReward = portInfo.SpecialRewards.FirstOrDefault();
            }
            else if (strategy.Type == StrategyType.Quota)
            {
                var allPortQuotas = GetCurrentPortQuotas();
                var incompleteQuota = allPortQuotas.FirstOrDefault(q => !q.IsCompleted);
                if (incompleteQuota != null)
                {
                    var portInfo = _portInfos.FirstOrDefault(p => p.Name == incompleteQuota.PortName);
                    if (portInfo != null) targetReward = portInfo.SpecialRewards.FirstOrDefault();
                }
            }

            List<PortInfo> relevantPorts = new List<PortInfo>();
            if (targetReward != null) { relevantPorts = _portInfos.Where(p => p.SpecialRewards.Contains(targetReward)).ToList(); }
            
            if (Settings.DebugLogging.Value)
            {
                LogMessage($"[Port Debug] Strategy: {strategy.Name}, Type: {strategy.Type}", 1);
                LogMessage($"[Port Debug] TargetReward: {targetReward}", 1);
                LogMessage($"[Port Debug] RelevantPorts Count: {relevantPorts.Count}", 1);
                if (relevantPorts.Any())
                {
                    LogMessage($"[Port Debug] RelevantPorts: {string.Join(", ", relevantPorts.Select(p => p.Name))}", 1);
                }
            }

            PortInfo shortestPort = null;
            PortInfo longestPort = null;

            if (relevantPorts.Any())
            {
                shortestPort = relevantPorts.OrderBy(p => p.Distance).First();
                longestPort = relevantPorts.OrderByDescending(p => p.Distance).First();
            }

            var longestColor = Color.Green;
            var shortestColor = Color.Yellow;

            for (int i = 1; i <= 5; i++)
            {
                var portElement = portMapContainer.GetChildAtIndex(i);
                if (portElement == null) continue;

                
                string portName = PortIndexMap.ContainsKey(i - 1) ? PortIndexMap[i - 1] : null;
                if (portName == null) continue;

                var rect = portElement.GetClientRect();

                if (portName == targetPortName)
                {
                    Graphics.DrawFrame(rect, Color.LawnGreen, 2);
                }

                if (strategy.Type == StrategyType.Automatic)
                {
                    if (Settings.DebugLogging.Value)
                    {
                        LogMessage($"[Port Debug] Inside Automatic block. Current Port: {portName}", 1);
                        LogMessage($"[Port Debug] Shortest Port: {shortestPort?.Name ?? "N/A"}, Longest Port: {longestPort?.Name ?? "N/A"}", 1);
                    }

                    if (shortestPort != null && portName == shortestPort.Name)
                    {
                        if (Settings.DebugLogging.Value) LogMessage($"[Port Debug] Condition met: Current Port is Shortest Port.", 1);
                        if (shortestPort.Name == longestPort.Name)
                        {
                            var label = "Best Reward";
                            var textSize = ImGui.CalcTextSize(label);
                            var textPos = new System.Numerics.Vector2(rect.Center.X - textSize.X / 2f, rect.Bottom + 5);
                            if (Settings.DebugLogging.Value) LogMessage($"[Port Debug] Drawing '{label}' at Pos: {textPos}, Size: {textSize}", 1);
                            Graphics.DrawTextWithBackground(label, textPos, Color.White, longestColor);
                        }
                        else
                        {
                            var label = "Shortest (Less Loot)";
                            var textSize = ImGui.CalcTextSize(label);
                            var textPos = new System.Numerics.Vector2(rect.Center.X - textSize.X / 2f, rect.Bottom + 5);
                            if (Settings.DebugLogging.Value) LogMessage($"[Port Debug] Drawing '{label}' at Pos: {textPos}, Size: {textSize}", 1);
                            Graphics.DrawTextWithBackground(label, textPos, Color.Black, shortestColor);
                        }
                    }
                    else if (longestPort != null && portName == longestPort.Name)
                    {
                        if (Settings.DebugLogging.Value) LogMessage($"[Port Debug] Condition met: Current Port is Longest Port.", 1);
                        var label = "Longest (More Loot)";
                        var textSize = ImGui.CalcTextSize(label);
                        var textPos = new System.Numerics.Vector2(rect.Center.X - textSize.X / 2f, rect.Bottom + 5);
                        if (Settings.DebugLogging.Value) LogMessage($"[Port Debug] Drawing '{label}' at Pos: {textPos}, Size: {textSize}", 1);
                        Graphics.DrawTextWithBackground(label, textPos, Color.White, longestColor);
                    }
                }
            }
        }
        
        private void DrawResourceSuggestions(Element container, Dictionary<string, int> suggestedGoals, Dictionary<string, int> addedResources)
        {
            if (container == null || !suggestedGoals.Any()) return;
            var invertedResourceMap = ResourceMap.ToDictionary(kp => kp.Value, kp => kp.Key);
            foreach (var goal in suggestedGoals)
            {
                string resourceName = goal.Key;
                if (!invertedResourceMap.ContainsKey(resourceName)) continue;
                int resourceIndex = invertedResourceMap[resourceName];
                var numberElement = container.GetChildAtIndex(resourceIndex)?.GetChildAtIndex(3);
                if (numberElement == null) continue;
                int addedAmount = addedResources.GetValueOrDefault(resourceName, 0);
                int requiredAmount = goal.Value;
                Color frameColor;
                if (requiredAmount <= 0) { frameColor = Color.Gray; }
                else
                {
                    float progress = (float)addedAmount / requiredAmount;
                    if (progress >= 1.0f) { frameColor = Color.LawnGreen; }
                    else if (progress >= 0.5f) { frameColor = Color.Yellow; }
                    else { frameColor = Color.Red; }
                }
                Graphics.DrawFrame(numberElement.GetClientRect(), frameColor, 2);
            }
        }

        private List<PortQuota> GetCurrentPortQuotas()
        {
            var quotas = new List<PortQuota>();
            var portRequests = GameController.Game.IngameState.IngameUi.VillageScreen?.PortRequests;
            if (portRequests == null) return quotas;
            for (int i = 0; i < portRequests.Count; i++)
            {
                if (!PortIndexMap.ContainsKey(i)) continue;
                var portData = portRequests[i];
                if (portData.Requests == null) continue;
                foreach (var request in portData.Requests)
                {
                    quotas.Add(new PortQuota
                    {
                        PortName = PortIndexMap[i],
                        ResourceName = request.ResourceType.Id,
                        DeliveredAmount = request.DeliveredAmount,
                        RequestedAmount = request.RequestedAmount
                    });
                }
            }
            return quotas;
        }

        private Dictionary<string, int> GetAddedResources(Element container)
        {
            var resources = new Dictionary<string, int>();
            if (container == null) return resources;
            for (int i = 5; i <= 15; i++)
            {
                if (!ResourceMap.ContainsKey(i)) continue;
                var addedAmountElement = container.GetChildAtIndex(i)?.GetChildAtIndex(2)?.GetChildAtIndex(0);
                if (addedAmountElement != null && int.TryParse(addedAmountElement.Text.Replace(",", ""), out int amount)) { resources[ResourceMap[i]] = amount; }
                else { resources[ResourceMap[i]] = 0; }
            }
            return resources;
        }

        private Dictionary<string, int> GetAvailableResources(Element container)
        {
            var resources = new Dictionary<string, int>();
            if (container == null) return resources;
            for (int i = 5; i <= 15; i++)
            {
                if (!ResourceMap.ContainsKey(i)) continue;
                var amountElement = container.GetChildAtIndex(i)?.GetChildAtIndex(3);
                if (amountElement != null && int.TryParse(amountElement.Text.Replace(",", ""), out int amount)) { resources[ResourceMap[i]] = amount; }
                else { resources[ResourceMap[i]] = 0; }
            }
            return resources;
        }
    }
}