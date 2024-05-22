﻿using VRageMath;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using VRage.Utils;
using Sandbox.ModAPI;

namespace BDAM
{
    class AssemblerWindow : HudElementBase
    {
        private readonly EmptyHudElement content;
        public WindowScrollContainer scrollContainer;

        public AssemblerWindow(HudParentBase parent) : base(parent)
        {
            AssemblerHud.Window = this;
            Size = new Vector2(1300, 700);
            Offset = new Vector2(0,0);
            scrollContainer = new WindowScrollContainer(this);

            Visible = false;
            UseCursor = true;
            ShareCursor = true;
        }

        public void ToggleVisibility(AssemblerComp AComp = null, bool closeOnly = false)
        {
            //TODO split out inv update and toggle vis to a task + callback, checking if the assembler's menu is already open elsewhere (new packet on open? close action on update?)
            if (closeOnly && !Visible)
                return;

            if(!Visible && Session.MPActive)
            {
                MyAPIGateway.Utilities.ShowNotification("Updating inventory...",500);
                AComp.gridComp.UpdateGrid();
                if(Session.logging)
                    MyLog.Default.WriteLineAndConsole($"{Session.modName} inventory update called on {AComp.gridComp.Grid.DisplayName}");
            }

            scrollContainer.aComp = AComp;
            Visible = !Visible;
            HudMain.EnableCursor = Visible;
            scrollContainer.Update(true);
            if(Visible)
                scrollContainer.title.Text = AComp.assembler.DisplayNameText + " on " + AComp.assembler.CubeGrid.DisplayName;
            if (!Visible && AComp != null)
                AComp.Save();
        }

        public void ToggleContentVisibility(bool value)
        {
            content.Visible = value;
        }

        public void SetContentToScroll()
        {
            scrollContainer.Visible = true;
        }
    }
}
