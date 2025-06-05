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
        private string labelStr;

        public AddItem(MyBlueprintDefinitionBase BP, String LabelStr, HudElementBase parent) : base(parent)
        {
            labelStr = LabelStr;
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

            labelBox = new TextBox(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Width = Size.X - addBox.Width,
                Offset = new Vector2(addBox.Width + 10, -5),
                Format = new GlyphFormat(Session.grey, TextAlignment.Left, 1f * Session.resMult),
                AutoResize = false,
                Text = labelStr,
                InputEnabled = false,
            };
        }
    }
}

