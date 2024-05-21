using System;
using VRageMath;
using RichHudFramework.UI;
using Sandbox.ModAPI;
using Sandbox.Definitions;
using VRage;

namespace BDAM
{
    public class QueueItem : HudElementBase
    {
        private readonly ListCompItem lComp;
        private readonly MyBlueprintDefinitionBase bp;
        private readonly TextBox labelBox, onHand;
        private readonly BorderedButton dBuild, dGrind, remove, buildAmount, grindAmount, priority;

        public QueueItem(ListCompItem LComp, MyBlueprintDefinitionBase BP, HudElementBase parent) : base(parent)
        {
            lComp = LComp;
            bp = BP;

            ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Left;
            Size = new Vector2(600, 25);

            labelBox = new TextBox(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Width = 300,
                Offset = new Vector2(20, 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1.25f),
                AutoResize = false,
                Text = lComp.label,
                InputEnabled = false,
            };

            buildAmount = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Offset = new Vector2(labelBox.Offset.X + labelBox.Width + 10, 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Right, 1.25f),
                AutoResize = false,
                Width = 140,
                Text = lComp.buildAmount <= -1 ? "---" : lComp.buildAmount.ToString(),
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            buildAmount.background.Width = buildAmount.Width;
            buildAmount.background.Padding = Vector2.Zero;

            dBuild = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Offset = new Vector2(buildAmount.Offset.X + buildAmount.Width + 5, 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f),
                AutoResize = false,
                Width = 20,
                Text = "X",
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            dBuild.background.Width = dBuild.Width;
            dBuild.background.Padding = Vector2.Zero;

            grindAmount = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Offset = new Vector2(dBuild.Offset.X + dBuild.Width + 15, 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Right, 1.25f),
                AutoResize = false,
                Width = 140,
                Text = lComp.grindAmount <= -1 ? "---" : lComp.grindAmount.ToString(),
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            grindAmount.background.Width = grindAmount.Width;
            grindAmount.background.Padding = Vector2.Zero;

            dGrind = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Offset = new Vector2(grindAmount.Offset.X + grindAmount.Width + 5, 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f),
                AutoResize = false,
                Width = 20,
                Text = "X",
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            dGrind.background.Width = dGrind.Width;
            dGrind.background.Padding = Vector2.Zero;

            onHand = new TextBox(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Offset = new Vector2(dGrind.Offset.X + dGrind.Width + 15, 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1.25f),
                AutoResize = false,
                Width = 140,
                Text = "Inv: 0",
                InputEnabled = false,
            };

            //Onhand qty
            var itemName = lComp.label;
            MyFixedPoint qty;
            if (AssemblerHud.Window.scrollContainer.aComp.gridComp.inventoryList.TryGetValue(itemName, out qty))
            {
                onHand.Text = "Inv: " + Session.NumberFormat(qty.ToIntSafe());
            }

            priority = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Offset = new Vector2(onHand.Offset.X + onHand.Width + 15, 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f),
                AutoResize = false,
                Width = 120,
                Text = "Pri: " + LComp.priority,
                ZOffset = 120,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            priority.background.Width = priority.Width;
            priority.background.Padding = Vector2.Zero;

            remove = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.Inner | ParentAlignments.Left,
                DimAlignment = DimAlignments.Height,
                Offset = new Vector2(priority.Offset.X + priority.Width + 15, 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f),
                AutoResize = false,
                Width = 120,
                Text = "Del",
                ZOffset = 120,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            remove.background.Width = remove.Width;
            remove.background.Padding = Vector2.Zero;


            //Inputs
            priority.MouseInput.LeftClicked += priLeftClicked;
            priority.MouseInput.RightClicked += priRightClicked;
            remove.MouseInput.LeftClicked += dLeftClicked;
            dBuild.MouseInput.LeftClicked += dLeftClicked;
            dGrind.MouseInput.LeftClicked += dLeftClicked;
            buildAmount.MouseInput.LeftClicked += LeftClicked;
            buildAmount.MouseInput.RightClicked += RightClicked;
            grindAmount.MouseInput.LeftClicked += LeftClicked;
            grindAmount.MouseInput.RightClicked += RightClicked;
        }

        private void priLeftClicked(object sender, EventArgs e)
        {
            lComp.priority--;
            if (lComp.priority < 1)
                lComp.priority = 1;
            priority.Text = "Pri: " + lComp.priority;
            lComp.dirty = true;
        }

        private void priRightClicked(object sender, EventArgs e)
        {
            lComp.priority++;
            if (lComp.priority > 3)
                lComp.priority = 3;
            priority.Text = "Pri: " + lComp.priority;
            lComp.dirty = true;
        }

        private void dLeftClicked(object sender, EventArgs e)
        {
            if (sender == dBuild)
            {
                lComp.buildAmount = -1;
                buildAmount.Text = "---";
                lComp.dirty = true;
            }
            else if (sender == dGrind)
            {
                lComp.grindAmount = -1;
                grindAmount.Text = "---";
                lComp.dirty = true;
            }
            else
            {
                AssemblerHud.Window.scrollContainer.RemoveQueueItem(bp);
            }
        }
        private void RightClicked(object sender, EventArgs e) 
        {
            UpdateQty(false, sender);
        }
        private void LeftClicked(object sender, EventArgs e)
        {
            UpdateQty(true, sender);
        }
        private void UpdateQty(bool increase, object sender)
        {
            var shift = MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.Shift); //100
            var control = MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.Control); //10
            var both = shift && control;

            var amount = both ? 1000 : shift ? 100 : control ? 10 : 1;
            if (!increase)
                amount *= -1;

            if (sender == buildAmount)
            {
                if(lComp.buildAmount == -1 && increase)
                    amount += 1;
                if (lComp.buildAmount + amount < 0)
                    lComp.buildAmount = -1;
                else if (lComp.buildAmount + amount >= int.MaxValue)
                    lComp.buildAmount = int.MaxValue;
                else
                    lComp.buildAmount += amount;
                buildAmount.Text = lComp.buildAmount <= -1 ? "---" : Session.NumberFormat(lComp.buildAmount);
            }
            else
            {
                if (lComp.grindAmount == -1 && increase && (shift || control))
                    amount += 1;
                if (lComp.grindAmount + amount < 0)
                    lComp.grindAmount = -1;
                else if (lComp.grindAmount + amount >= int.MaxValue)
                    lComp.grindAmount = int.MaxValue;
                else
                    lComp.grindAmount += amount;
                grindAmount.Text = lComp.grindAmount <= -1 ? "---" : Session.NumberFormat(lComp.grindAmount);
            }
            lComp.dirty = true;
        }
    }
}

