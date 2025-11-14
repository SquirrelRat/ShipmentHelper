using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Collections.Generic;
using Newtonsoft.Json; 

using static ShipmentHelper.ShipmentHelper;

namespace ShipmentHelper
{
    public class ShipmentHelperSettings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(true);

        [Menu("Debug")]
        public ToggleNode DebugLogging { get; set; } = new ToggleNode(false);

        [Menu("Shipment Helper HUD/Show Helper Hud")]
        public ToggleNode ShowHelperHud { get; set; } = new ToggleNode(true);
        
        internal RangeNode<float> HudSavedPosX { get; set; } = new RangeNode<float>(0.5f, 0, 1);
        internal RangeNode<float> HudSavedPosY { get; set; } = new RangeNode<float>(0.5f, 0, 1);

        [Menu("Shipment Timer Window/Show Shipment Timers")]
        public ToggleNode ShowShipmentTimers { get; set; } = new ToggleNode(true);

        internal RangeNode<float> TimerPosX { get; set; } = new RangeNode<float>(0.02f, 0, 1);
        internal RangeNode<float> TimerPosY { get; set; } = new RangeNode<float>(0.5f, 0, 1);
        
        [Menu("Strategy Management/Target Strategy")]
        public ListNode TargetStrategy { get; set; } = new ListNode();

        [Menu("Strategy Management/Always Suggest Dust")]
        public ToggleNode AlwaysSuggestDust { get; set; } = new ToggleNode(false);

        [Menu("Strategy Management/Automatic % of Resources to Send")]
        public ListNode AutomaticPercentageToSend { get; set; } = new ListNode() 
        { 
            Values = { "25%", "50%", "75%", "100%" }, 
            Value = "75%" 
        };
        
        [Menu("Strategy Management/Custom Strategies/My Custom Strategies")]
        public List<ShippingStrategy> CustomStrategies { get; set; } = new List<ShippingStrategy>();
        
        [Menu("Strategy Management/Custom Strategies/Add New Custom Strategy")]
        public ButtonNode AddNewStrategy { get; set; } = new ButtonNode();
        
        internal List<ShippingStrategy> AllStrategies { get; } = new List<ShippingStrategy>();
    }
}