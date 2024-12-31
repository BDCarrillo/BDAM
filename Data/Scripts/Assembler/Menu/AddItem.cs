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
            Size = new Vector2(300 * Session.resMult, 30 * Session.resMult);

            addBox = new BorderedCheckBox(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                Offset = new Vector2(5, 0),
                Width = 20 * Session.resMult,
                Height = 20 * Session.resMult,
                UseFocusFormatting = false,
                IsBoxChecked = false,
            };
            addBox.MouseInput.LeftClicked += MouseInput_LeftClicked;

            labelBox = new TextBox(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Width = Size.X - addBox.Width,
                Offset = new Vector2(addBox.Width + 10, -5),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1f * Session.resMult),
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

