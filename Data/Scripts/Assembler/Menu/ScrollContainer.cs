using System;
using VRageMath;
using RichHudFramework.UI;
using Sandbox.ModAPI;
using System.Collections.Generic;
using Sandbox.Definitions;

namespace BDAM
{
    public class WindowScrollContainer : HudElementBase
    {
        public readonly LabelBox title;
        public readonly TextBox labelBuildQty, labelGrindQty;
        private readonly BorderedButton close, add, clearAll, addAll;
        public List<QueueItem> queueList = new List<QueueItem>();
        internal AssemblerComp aComp;
        internal int startPos = 0;
        internal int listLen = 20;

        public WindowScrollContainer(HudParentBase parent) : base(parent)
        {
            DimAlignment = DimAlignments.Both;
            IsMasking = true;
            //Main BG
            new TexturedBox(this)
            {
                Color = new Color(41, 54, 62, 220),
                DimAlignment = DimAlignments.Both,
                ParentAlignment = ParentAlignments.Center,              
            };       
            

            title = new LabelBox(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner,
                DimAlignment = DimAlignments.Width,
                Height = 60f,
                Color = new Color(41, 54, 62),
                Padding = new Vector2(10,0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1.5f, RichHudFramework.UI.Rendering.FontStyles.Underline),
                VertCenterText = false,
                AutoResize = false,
                ZOffset = 2,
            };

            new TextBox(title)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Left,
                Width = 300,
                Height = 30f,
                Offset = new Vector2(10, -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1.25f),
                AutoResize = false,
                Text = "Item Name",
                InputEnabled = false,
            };
            new TextBox(title)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Left,
                Width = 300,
                Height = 30f,
                Offset = new Vector2(325, -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1.25f),
                AutoResize = false,
                Text = "Build to Qty",
                InputEnabled = false,
            };
            new TextBox(title)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Left,
                Width = 300,
                Height = 30f,
                Offset = new Vector2(465, -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1.25f),
                AutoResize = false,
                Text = "Grind to Qty",
                InputEnabled = false,
            };

            close = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = 30,
                Offset = new Vector2(10, -10),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 2f),
                AutoResize = false,
                Width = 100,
                Text = "X",
                ZOffset = 50,
                TextPadding = new Vector2(8, 8),
                UseFocusFormatting = false,
            };
            close.background.Width = close.Width;

            add = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = 30,
                Offset = new Vector2(-220, -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f),
                AutoResize = false,
                Width = 140,
                Text = "Add",
                ZOffset = 50,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),

            };
            add.background.Width = add.Width;
            add.background.Padding = Vector2.Zero;


            addAll = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = 30,
                Offset = new Vector2(add.Offset.X + add.Width, -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f),
                AutoResize = false,
                Width = 140,
                Text = "Add All",
                ZOffset = 50,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),

            };
            addAll.background.Width = addAll.Width;
            addAll.background.Padding = Vector2.Zero;
           
            clearAll = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = 30,
                Offset = new Vector2(addAll.Offset.X + addAll.Width, -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f),
                AutoResize = false,
                Width = 140,
                Text = "Clear All",
                ZOffset = 50,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8,0),
                Padding = new Vector2(8, 0),

            };
            clearAll.background.Width = clearAll.Width;
            clearAll.background.Padding = Vector2.Zero;

            //Controls
            addAll.MouseInput.LeftClicked += LeftClick;
            clearAll.MouseInput.LeftClicked += LeftClick;
            add.MouseInput.LeftClicked += LeftClick;
            close.MouseInput.LeftClicked += LeftClick;
            //TODO show automation on/off
            //TODO button for inventory summary?
        }

        private void LeftClick(object sender, EventArgs e)
        {
            if(sender ==  close) 
                AssemblerHud.Window.ToggleVisibility(aComp);
            else if(sender == addAll)
            {
                ListComp tempListComp = new ListComp() { compItems = new List<ListCompItem>() };
                foreach (var bp in Session.assemblerBP2[aComp.assembler.BlockDefinition.SubtypeId])
                {
                    if(!aComp.buildList.ContainsKey(bp))
                    {
                        var tempListCompItem = new ListCompItem() { bpBase = bp.Id.SubtypeName, buildAmount = -1, grindAmount = -1 };
                        aComp.buildList.Add(bp, tempListCompItem);
                    }
                }
                Update(true);
            }
            else if (sender == clearAll) 
            {
                Clear();
            }
            else if (sender == add)
            {
                //TODO open submenu for selection of available BPs

            }                   
        }

        public void RemoveQueueItem(MyBlueprintDefinitionBase key)
        {
            aComp.buildList.Remove(key);
            Update(true);
        }

        public void Update(bool rebuild = false)
        {
            if (rebuild)
            {
                Clear();
                //TODO Buildlist alphabetical sorting?
                foreach (var listItem in aComp.buildList)
                {
                    var qItem = new QueueItem(listItem.Value, listItem.Key, this);
                    queueList.Add(qItem);

                }
            }
            
            float offset = -65;

            //queuelist stacking to simulate a scroll list
            for(int i = 0; i < queueList.Count; i++) 
            {
                var qItem = queueList[i];
                if (i < startPos)
                {
                    qItem.Visible = false;
                    continue;
                }
                qItem.Visible = true;
                qItem.Offset = new Vector2(-8, offset);
                offset -= qItem.Size.Y;
            }
        }

        private void Clear()
        {
            while (queueList.Count > 0)
            {
                queueList[0].Unregister();
                queueList.RemoveAt(0);
            }
        }

        protected override void HandleInput(Vector2 cursorPos)
        {
            if (Visible && queueList.Count > listLen)
            {
                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if (scroll != 0)
                {
                    if (scroll < 0 && startPos < queueList.Count - listLen)
                        startPos++;
                    else if (scroll > 0 && startPos > 0)
                        startPos--;
                    Update();
                }
            }
        }
    }
}
