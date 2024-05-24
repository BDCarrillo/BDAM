using System;
using VRageMath;
using RichHudFramework.UI;
using Sandbox.ModAPI;
using Sandbox.Definitions;
using VRage;

namespace BDAM
{
    public class AddItem : HudElementBase
    {
        private readonly MyBlueprintDefinitionBase bp;
        private readonly TextBox labelBox;
        private readonly BorderedButton checkBox;

        public AddItem(MyBlueprintDefinitionBase BP, HudElementBase parent) : base(parent)
        {
            bp = BP;

            ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Left;
            Size = new Vector2(300, 640);

            checkBox = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Offset = new Vector2(0, 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Right, 1.25f),
                AutoResize = false,
                Width = 140,
                Text = "X",
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            checkBox.background.Width = checkBox.Width;
            checkBox.background.Padding = Vector2.Zero;

            labelBox = new TextBox(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Width = 300,
                Offset = new Vector2(20, 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1.25f),
                AutoResize = false,
                Text = bp.Results[0].Id.SubtypeName,
                InputEnabled = false,
            };

            //Inputs
            checkBox.MouseInput.LeftClicked += LeftClicked;

        }
        private void LeftClicked(object sender, EventArgs e)
        {

        }
    }
}

