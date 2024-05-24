using System;
using VRageMath;
using RichHudFramework.UI;
using Sandbox.Definitions;

namespace BDAM
{
    public class AddItem : HudElementBase
    {
        public readonly MyBlueprintDefinitionBase bp;
        private readonly TextBox labelBox;
        public readonly BorderedCheckBox addBox;
        public bool boxChecked = false;

        public AddItem(MyBlueprintDefinitionBase BP, HudElementBase parent) : base(parent)
        {
            bp = BP;

            ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Left;
            Size = new Vector2(300, 30);

            addBox = new BorderedCheckBox(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                Offset = new Vector2(5, 0),
                Width = 20,
                Height = 20,
                UseFocusFormatting = false,
                IsBoxChecked = false,
            };
            addBox.MouseInput.LeftClicked += MouseInput_LeftClicked;

            labelBox = new TextBox(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Width = 280,
                Offset = new Vector2(30, -5),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1f),
                AutoResize = false,
                Text = bp.Results[0].Id.SubtypeName,
                InputEnabled = false,
            };
        }

        private void MouseInput_LeftClicked(object sender, EventArgs e)
        {
            boxChecked = addBox.IsBoxChecked;
        }
    }
}

