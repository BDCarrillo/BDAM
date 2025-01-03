using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.ModAPI;
using System;
using VRage.Utils;
using System.Collections.Generic;
using VRage.Game.Components;
using Sandbox.Game.Entities;
using System.Text;

namespace BDAM
{
    public partial class Session : MySessionComponentBase
    {
        private List<IMyTerminalControl> _customControls = new List<IMyTerminalControl>();
        internal readonly HashSet<IMyTerminalAction> _customActions = new HashSet<IMyTerminalAction>();
        internal void CreateTerminalControls<T>() where T : IMyAssembler
        {
            _customControls.Add(Separator<T>());
            _customControls.Add(AddOnOff<T>("queueOnOff", "Automatic Control", "", "On", "Off", GetActivated, SetActivated, CheckMode, VisibleTrue));
            _customControls.Add(AddButton<T>("showInv", "Inventory Summary", "Inventory Summary", OpenSummary, VisibleTrue, VisibleTrue));
            _customControls.Add(AddButton<T>("sortInv", "Combine Item Stacks", "Combine items stacks on this grid (within each cargo container)", CombineItemStacks, VisibleTrue, VisibleTrue));
            _customControls.Add(AddButton<T>("exportQueue", "Export To Custom Data", "Export BDAM queue to custom data", ExportCustomData, VisibleTrue, VisibleTrue));
            _customControls.Add(AddButton<T>("importQueue", "Import From Custom Data", "Import BDAM queue updates/additions from custom data", ImportCustomData, VisibleTrue, VisibleTrue));
            _customControls.Add(Separator<T>());
            _customActions.Add(CreateAssemblerMenuAction<T>());
            _customActions.Add(CreateAssemblerAutoAction<T>());

            foreach (var action in _customActions)
                MyAPIGateway.TerminalControls.AddAction<T>(action);
            foreach (var control in _customControls)
                MyAPIGateway.TerminalControls.AddControl<T>(control);
        }
        public static string NumberFormat(float number)
        {
            var numStr = ((int)number).ToString();
            var numLen = numStr.Length;
            if (numLen > 3 && numLen <= 6)//Thousands
                numStr = string.Format("{0:#,##0.##}", number / 1000) + " K";
            else if (numLen > 6)//Millions
                numStr = string.Format("{0:#,##0.##}", number / 1000000) + " M";
            return numStr;
        }
        internal IMyTerminalControlSeparator Separator<T>() where T : IMyAssembler
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>("Assembler_Separator");
            c.Enabled = IsTrue;
            c.Visible = IsTrue;
            return c;
        }
        internal bool IsTrue(IMyTerminalBlock block)
        {
            return true;
        }
        internal static IMyTerminalControlButton AddButton<T>(string name, string title, string tooltip, Action<IMyTerminalBlock> action, Func<IMyTerminalBlock, bool> enableGetter = null, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>(name);
            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Visible = visibleGetter;
            c.Enabled = enableGetter;
            c.Action = action;
            return c;
        }
        internal static IMyTerminalControlOnOffSwitch AddOnOff<T>(string name, string title, string tooltip, string onText, string offText, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, Func<IMyTerminalBlock, bool> enabledGetter = null, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>(name);
            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.OnText = MyStringId.GetOrCompute(onText);
            c.OffText = MyStringId.GetOrCompute(offText);
            c.Enabled = enabledGetter;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            return c;
        }
        public static bool VisibleTrue(IMyTerminalBlock block)
        {
            return true;
        }
        private static bool CheckMode(IMyTerminalBlock block)
        {
            var assembler = block as IMyAssembler;
            if (!assembler.Enabled || assembler.CooperativeMode || !assembler.UseConveyorSystem)
                return false;
            else
                return true;
        }
        internal bool GetActivated(IMyTerminalBlock block)
        {
            var assembler = block as IMyAssembler;
            if (assembler.CooperativeMode)
                return false;

            GridComp gComp;
            AssemblerComp aComp;
            if (GridMap.TryGetValue(block.CubeGrid, out gComp) && gComp.assemblerList.TryGetValue((MyCubeBlock)block, out aComp))
                return aComp.autoControl;
            return false;
        }
        internal void SetActivated(IMyTerminalBlock block, bool activated)
        {
            GridComp gComp;
            AssemblerComp aComp;
            if (GridMap.TryGetValue(block.CubeGrid, out gComp) && gComp.assemblerList.TryGetValue((MyCubeBlock)block, out aComp))
            {
                aComp.autoControl = !aComp.autoControl;
                if (MPActive)
                {
                    if (netlogging) Log.WriteLine(modName + $"Sending updated auto control state to server " + aComp.autoControl);
                    var packet = new UpdateStatePacket { Var = UpdateType.autoControl, Value = aComp.autoControl ? 1 : 0, Type = PacketType.UpdateState, EntityId = aComp.assembler.EntityId };
                    SendPacketToServer(packet);
                }
            }
            return;
        }
        internal void OpenAssemblerMenu(IMyTerminalBlock block)
        {
            GridComp gComp;
            AssemblerComp aComp;
            if (GridMap.TryGetValue(block.CubeGrid, out gComp) && gComp.assemblerList.TryGetValue((MyCubeBlock)block, out aComp))
            {
                aWindow.ToggleVisibility(aComp);
                openAComp = aComp;
                MyAPIGateway.Session.Player.Controller.ControlledEntityChanged += GridChange;
            }
        }
        private void GridChange(VRage.Game.ModAPI.Interfaces.IMyControllableEntity entity1, VRage.Game.ModAPI.Interfaces.IMyControllableEntity entity2)
        {
            aWindow.ToggleVisibility(openAComp, true);
            MyAPIGateway.Session.Player.Controller.ControlledEntityChanged -= GridChange;
        }
        internal void SetAutoMode(IMyTerminalBlock block)
        {
            GridComp gComp;
            AssemblerComp aComp;
            if (GridMap.TryGetValue(block.CubeGrid, out gComp) && gComp.assemblerList.TryGetValue((MyCubeBlock)block, out aComp))
            {
                aComp.autoControl = !aComp.autoControl;
                if (MPActive)
                {
                    if (netlogging) Log.WriteLine(modName + $"Sending updated auto control state to server " + aComp.autoControl);
                    var packet = new UpdateStatePacket { Var = UpdateType.autoControl, Value = aComp.autoControl ? 1 : 0, Type = PacketType.UpdateState, EntityId = aComp.assembler.EntityId };
                    SendPacketToServer(packet);
                }
            }
        }
        internal IMyTerminalAction CreateAssemblerMenuAction<T>() where T : IMyAssembler
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>("AssemblerMenu");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action.Name = new StringBuilder("Open Assembler Menu");
            action.Action = OpenAssemblerMenu;
            action.Writer = MenuActionWriter;
            action.Enabled = IsTrue;
            action.ValidForGroups = false;
            return action;
        }
        internal IMyTerminalAction CreateAssemblerAutoAction<T>() where T : IMyAssembler
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>("AssemblerAutoMode");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder("Assembler Auto Mode On/Off");
            action.Action = SetAutoMode;
            action.Writer = AutoActionWriter;
            action.Enabled = NotCoOp;
            action.ValidForGroups = false;
            return action;
        }
        internal void MenuActionWriter(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append("Menu");
        }
        internal void AutoActionWriter(IMyTerminalBlock block, StringBuilder builder)
        {
            bool auto = false;
            AssemblerComp aComp;            
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
            {
                if (aComp.assembler.CooperativeMode)
                    auto = false;
                else
                    auto = aComp.autoControl;
            }
            builder.Append("Auto: " + (auto ? "On" : "Off"));
        }
        internal bool NotCoOp(IMyTerminalBlock block)
        {
            AssemblerComp aComp;
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
            {
                if (aComp.assembler.CooperativeMode)
                {
                    aComp.autoControl = false;
                    return false;
                }
                else
                    return true;
            }
            return false;
        }
    }
}