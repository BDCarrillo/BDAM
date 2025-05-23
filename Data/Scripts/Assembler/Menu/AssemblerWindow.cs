using VRageMath;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;

namespace BDAM
{
    public class AssemblerWindow : HudElementBase
    {
        public WindowScrollContainer scrollContainer;
        public bool initted;
        public Session _session;
        public AssemblerWindow(HudParentBase parent, Session session) : base(parent)
        {
            //Size = new Vector2(1300, 700);
            Offset = new Vector2(0,0);
            //scrollContainer = new WindowScrollContainer(this);
            _session = session;
            Visible = false;
            UseCursor = true;
            ShareCursor = true;
        }
        public void ToggleVisibility(AssemblerComp aComp = null, bool closeOnly = false)
        {
            //TODO split out inv update and toggle vis to a task + callback, checking if the assembler's menu is already open elsewhere (new packet on open? close action on update?)
            if (closeOnly && !Visible)
                return;

            //scaling
            if (!initted)
            {
                var vpY = MyAPIGateway.Session.Camera.ViewportSize.Y;
                Session.resMult = (vpY - 1080) / 1080 * 0.5f + 1;
                Size = new Vector2(1300 * Session.resMult, 700 * Session.resMult);
                scrollContainer = new WindowScrollContainer(this, _session);
                initted = true;
            }

            if (!Visible && Session.MPActive)
            {
                MyAPIGateway.Utilities.ShowNotification("Updating inventory...", 500);
                aComp.gridComp.UpdateGrid();
            }
            scrollContainer.aComp = aComp;
            Visible = !Visible;
            HudMain.EnableCursor = Visible;
            scrollContainer.Update(true);
            if(Visible)
                scrollContainer.title.Text = aComp.assembler.DisplayNameText + " on " + aComp.assembler.CubeGrid.DisplayName;
            if (!Visible && aComp != null)
                aComp.Save();
        }
    }
}
