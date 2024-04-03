using VRageMath;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;

namespace BDAM
{
    class AssemblerWindow : HudElementBase
    {
        private readonly EmptyHudElement content;
        public WindowScrollContainer scrollContainer;

        public AssemblerWindow(HudParentBase parent) : base(parent)
        {
            AssemblerHud.Window = this;
            Size = new Vector2(HudMain.ScreenWidth * 0.5f + 0.1f, HudMain.ScreenHeight * 0.6f);
            Offset = new Vector2(0,0);
            scrollContainer = new WindowScrollContainer(this);


            Visible = false;
            UseCursor = true;
            ShareCursor = true;
        }

        public void ToggleVisibility(AssemblerComp AComp = null)
        {
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
