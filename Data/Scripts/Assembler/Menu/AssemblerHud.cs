using System;
using VRageMath;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using System.Collections.Generic;
using RichHudFramework.Client;
using VRage.Game.Components;
using Sandbox.Definitions;
using VRage;

namespace BDAM
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class AssemblerHud : MySessionComponentBase
    {

        public static AssemblerWindow Window;

        public static void Init()
        {
            if (Window == null && !MyAPIGateway.Utilities.IsDedicated)
                new AssemblerHud();
        }

        protected AssemblerHud()
        {
            RichHudClient.Init("Assembler", HudInit, ClientReset);
        }

        private void HudInit()
        {
            RichHudTerminal.Root.Enabled = true;
            new AssemblerWindow(HudMain.Root);
        }      

        private void ClientReset() { }


    }
}
