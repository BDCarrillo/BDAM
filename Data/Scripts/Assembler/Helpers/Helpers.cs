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
        public static string FriendlyNameLookup(string name)
        {
            if (NameLookupFriendly.ContainsKey(name))
                return NameLookupFriendly[name];
            return name;
        }
        internal void CreateTerminalControls<T>() where T : IMyAssembler
        {
            MyAPIGateway.TerminalControls.AddControl<T>(Separator<T>());
            MyAPIGateway.TerminalControls.AddControl<T>(AddOnOff<T>("queueOnOff", "BDAM Auto Queue Control", "Enables BDAM automatic queue control, as set\nin the assembler window accessed via hotbar", "On", "Off", GetAutoOnOff, SetAutoOnOff, CheckModeAuto, VisibleTrue));
            MyAPIGateway.TerminalControls.AddControl<T>(AddOnOff<T>("masterOnOff", "BDAM Master Mode", "Designates this assembler (max 1 per grid) as the Master\nwhich will balance large quantities or slow queued items\nout to available helpers", "On", "Off", GetMasterOnOff, SetMasterOnOff, CheckModeMaster, VisibleTrue));
            MyAPIGateway.TerminalControls.AddControl<T>(AddOnOff<T>("helperOnOff", "BDAM Helper Mode", "Determines if assembler will help with designated\nMaster assembler with its build or grind queue", "On", "Off", GetHelperOnOff, SetHelperOnOff, CheckModeHelper, VisibleTrue, true));
            MyAPIGateway.TerminalControls.AddControl<T>(AddButton<T>("showInv", "Inventory Summary", "Inventory Summary", OpenSummary, VisibleTrue, VisibleTrue));
            MyAPIGateway.TerminalControls.AddControl<T>(AddButton<T>("sortInv", "Combine Item Stacks", "Combine items stacks on this grid (within each cargo container)", CombineItemStacks, VisibleTrue, VisibleTrue));
            MyAPIGateway.TerminalControls.AddControl<T>(AddButton<T>("exportQueue", "Export To Custom Data", "Export BDAM queue to custom data", ExportCustomData, VisibleTrue, VisibleTrue));
            MyAPIGateway.TerminalControls.AddControl<T>(AddButton<T>("importQueue", "Import From Custom Data", "Import BDAM queue updates/additions from custom data", ImportCustomData, VisibleTrue, VisibleTrue));
            MyAPIGateway.TerminalControls.AddControl<T>(Separator<T>());

            CreateAssemblerMenuAction<T>();
            CreateAssemblerAutoAction<T>();
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
        internal static IMyTerminalControlOnOffSwitch AddOnOff<T>(string name, string title, string tooltip, string onText, string offText, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, Func<IMyTerminalBlock, bool> enabledGetter = null, Func<IMyTerminalBlock, bool> visibleGetter = null, bool multi = false) where T : IMyTerminalBlock
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
            c.SupportsMultipleBlocks = multi;
            return c;
        }
        public static bool VisibleTrue(IMyTerminalBlock block)
        {
            return true;
        }
        private bool CheckModeAuto(IMyTerminalBlock block)
        {
            var assembler = block as IMyAssembler;
            if (!assembler.Enabled || assembler.CooperativeMode || !assembler.UseConveyorSystem)
                return false;
            else
            {
                AssemblerComp aComp;
                if (aCompMap.TryGetValue(block.EntityId, out aComp))
                    return !aComp.helperMode;
                return false;
            }
        }
        private bool CheckModeMaster(IMyTerminalBlock block)
        {
            var assembler = block as IMyAssembler;
            if (!assembler.Enabled || assembler.CooperativeMode || !assembler.UseConveyorSystem)
                return false;
            else
            {
                GridComp gComp;
                AssemblerComp aComp;
                if (GridMap.TryGetValue(block.CubeGrid, out gComp) && gComp.assemblerList.TryGetValue((MyCubeBlock)block, out aComp))
                    if (gComp.masterAssembler == aComp.assembler.EntityId || gComp.masterAssembler == 0)
                        return !aComp.helperMode;
                return false;
            }
        }
        private bool CheckModeHelper(IMyTerminalBlock block)
        {
            var assembler = block as IMyAssembler;
            if (!assembler.Enabled || assembler.CooperativeMode || !assembler.UseConveyorSystem)
                return false;
            else
            {
                AssemblerComp aComp;
                if (aCompMap.TryGetValue(block.EntityId, out aComp))
                    return !(aComp.autoControl || aComp.masterMode);
                return false;
            }
        }
        internal bool GetAutoOnOff(IMyTerminalBlock block)
        {
            AssemblerComp aComp;
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
            {
                if(aComp.assembler.CooperativeMode || !aComp.assembler.UseConveyorSystem)
                {
                    aComp.helperMode = false;
                    aComp.masterMode = false;
                    aComp.autoControl = false;
                    var packet = new UpdateStatePacket { Var = UpdateType.reset, Value = 0, Type = PacketType.UpdateState, EntityId = aComp.assembler.EntityId };
                    SendPacketToServer(packet);
                }
                return aComp.autoControl;
            }
            return false;
        }
        internal bool GetMasterOnOff(IMyTerminalBlock block)
        {
            AssemblerComp aComp;
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
                return aComp.masterMode;
            return false;
        }
        internal bool GetHelperOnOff(IMyTerminalBlock block)
        {
            AssemblerComp aComp;
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
                return aComp.helperMode;
            return false;
        }
        internal void SetAutoOnOff(IMyTerminalBlock block, bool activated)
        {
            AssemblerComp aComp;
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
            {
                aComp.autoControl = !aComp.autoControl;
                if (aComp.autoControl)
                    aComp.helperMode = false;
                aComp.assembler.ShowInInventory = !aComp.assembler.ShowInInventory;
                aComp.assembler.ShowInInventory = !aComp.assembler.ShowInInventory;
                if (netlogging) Log.WriteLine(modName + $"Sending updated auto control state to server for {aComp.assembler.CustomName} " + aComp.autoControl);
                var packet = new UpdateStatePacket { Var = UpdateType.autoControl, Value = aComp.autoControl ? 1 : 0, Type = PacketType.UpdateState, EntityId = aComp.assembler.EntityId };
                SendPacketToServer(packet);
            }
            return;
        }
        internal void SetMasterOnOff(IMyTerminalBlock block, bool activated)
        {
            GridComp gComp;
            AssemblerComp aComp;
            if (GridMap.TryGetValue(block.CubeGrid, out gComp) && gComp.assemblerList.TryGetValue((MyCubeBlock)block, out aComp))
            {
                aComp.masterMode = !aComp.masterMode;
                if (aComp.masterMode)
                    aComp.helperMode = false;
                aComp.assembler.ShowInInventory = !aComp.assembler.ShowInInventory;
                aComp.assembler.ShowInInventory = !aComp.assembler.ShowInInventory;
                if (netlogging) Log.WriteLine(modName + $"Sending updated master control state to server for {aComp.assembler.CustomName} " + aComp.masterMode);
                var packet = new UpdateStatePacket { Var = UpdateType.masterMode, Value = aComp.masterMode ? 1 : 0, Type = PacketType.UpdateState, EntityId = aComp.assembler.EntityId };
                SendPacketToServer(packet);
            }
            return;
        }
        internal void SetHelperOnOff(IMyTerminalBlock block, bool activated)
        {
            AssemblerComp aComp;            
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
            {
                aComp.helperMode = !aComp.helperMode;
                if (aComp.helperMode)
                {
                    aComp.masterMode = false;
                    aComp.autoControl = false;
                }
                aComp.assembler.ShowInInventory = !aComp.assembler.ShowInInventory;
                aComp.assembler.ShowInInventory = !aComp.assembler.ShowInInventory;
                if (netlogging) Log.WriteLine(modName + $"Sending updated helper control state to server for {aComp.assembler.CustomName} " + aComp.helperMode);
                var packet = new UpdateStatePacket { Var = UpdateType.helperMode, Value = aComp.helperMode ? 1 : 0, Type = PacketType.UpdateState, EntityId = aComp.assembler.EntityId };
                SendPacketToServer(packet);
            }
            return;
        }
        internal void OpenAssemblerMenu(IMyTerminalBlock block)
        {
            AssemblerComp aComp;
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
            {
                aWindow.ToggleVisibility(aComp);
                openAComp = aComp;
                MyAPIGateway.Session.Player.Controller.ControlledEntityChanged += GridChange;
            }
        }
        internal void SetAutoMode(IMyTerminalBlock block)
        {
            AssemblerComp aComp;
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
            {
                aComp.autoControl = !aComp.autoControl;
                if (netlogging) Log.WriteLine(modName + $"Sending updated auto control state to server for {aComp.assembler.CustomName} " + aComp.autoControl);
                var packet = new UpdateStatePacket { Var = UpdateType.autoControl, Value = aComp.autoControl ? 1 : 0, Type = PacketType.UpdateState, EntityId = aComp.assembler.EntityId };
                SendPacketToServer(packet);
            }
        }
        internal void CreateAssemblerMenuAction<T>() where T : IMyAssembler
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>("AssemblerMenu");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action.Name = new StringBuilder("BDAM Open Assembler Menu");
            action.Action = OpenAssemblerMenu;
            action.Writer = MenuActionWriter;
            action.Enabled = IsTrue;
            action.ValidForGroups = false;
            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }
        internal void CreateAssemblerAutoAction<T>() where T : IMyAssembler
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>("AssemblerAutoMode");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder("BDAM Assembler Auto Mode On/Off");
            action.Action = SetAutoMode;
            action.Writer = AutoActionWriter;
            action.Enabled = NotCoOp;
            action.ValidForGroups = false;
            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }
        internal void MenuActionWriter(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append("Menu");
        }
        internal void AutoActionWriter(IMyTerminalBlock block, StringBuilder builder)
        {
            AssemblerComp aComp;            
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
                builder.Append("Auto: " + (aComp.autoControl ? "On" : "Off"));
        }
        internal bool NotCoOp(IMyTerminalBlock block)
        {
            AssemblerComp aComp;
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
                return !(aComp.assembler.CooperativeMode || aComp.helperMode);
            return false;
        }
    }
}