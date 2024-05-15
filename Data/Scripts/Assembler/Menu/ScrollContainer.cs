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
        private readonly BorderedButton close, add, clearAll, addAll, autoMode, summary;
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
            //Labels
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

            //Corner X
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

            //Buttons: Top row
            autoMode = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = 30,
                Offset = new Vector2(-220, 0),// -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f),
                AutoResize = false,
                Width = 140,
                Text = "Auto: ---",
                ZOffset = 50,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),

            };
            autoMode.background.Width = autoMode.Width;
            autoMode.background.Padding = Vector2.Zero;

            summary = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = 30,
                Offset = new Vector2(autoMode.Offset.X + autoMode.Width, 0),// -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f),
                AutoResize = false,
                Width = 140,
                Text = "Summary",
                ZOffset = 50,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),

            };
            summary.background.Width = summary.Width;
            summary.background.Padding = Vector2.Zero;

            //Buttons: Bottom row
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
            summary.MouseInput.LeftClicked += LeftClick;
            autoMode.MouseInput.LeftClicked += LeftClick;
            addAll.MouseInput.LeftClicked += LeftClick;
            clearAll.MouseInput.LeftClicked += LeftClick;
            add.MouseInput.LeftClicked += LeftClick;
            close.MouseInput.LeftClicked += LeftClick;
        }

        private void LeftClick(object sender, EventArgs e)
        {           
            if(sender == close) 
                AssemblerHud.Window.ToggleVisibility(aComp);
            else if(sender == addAll)
            {
                ListComp tempListComp = new ListComp() { compItems = new List<ListCompItem>() };
                foreach (var bp in Session.assemblerBP2[aComp.assembler.BlockDefinition.SubtypeId])
                {
                    if(!aComp.buildList.ContainsKey(bp))
                    {
                        var tempListCompItem = new ListCompItem() { bpBase = bp.Id.SubtypeName, label = bp.Results[0].Id.SubtypeName };
                        aComp.buildList.Add(bp, tempListCompItem);
                    }
                }
                Update(true);
            }
            else if (sender == clearAll) 
            {
                Clear(true);
            }
            else if (sender == autoMode)
            {
                aComp.autoControl = !aComp.autoControl;
                autoMode.Text = "Auto: " + (aComp.autoControl ? "On" : "Off");
            }
            else if (sender == summary)
            {
                Session.OpenSummary(aComp.assembler);
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
                autoMode.Text = "Auto: " + (aComp.autoControl ? "On" : "Off");
                Clear();

                //Buildlist alphabetical sorting
                var sortedList = new List<string>();
                var refDict = new Dictionary<string, QueueItem>();
                foreach (var item in aComp.buildList)
                {
                    var qItem = new QueueItem(item.Value, item.Key, this);
                    sortedList.Add(item.Value.label);
                    refDict.Add(item.Value.label, qItem);
                }
                sortedList.Sort();

                foreach (var item in sortedList)
                {
                    queueList.Add(refDict[item]);
                }

                //Plain addition w/o sorting
                /*
                foreach (var listItem in aComp.buildList)
                {
                    var qItem = new QueueItem(listItem.Value, listItem.Key, this);
                    queueList.Add(qItem);
                }
                */                
            }
            
            //Starting offset to get scrollbox list items below header bar
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
                offset -= qItem.Size.Y + 5; //+5 for add'l spacing between rows
            }
        }

        private void Clear(bool delete = false)
        {
            if(delete)
            {
                aComp.buildList.Clear();
            }
            while (queueList.Count > 0)
            {
                queueList[0].Unregister();
                queueList.RemoveAt(0);
            }
        }

        //Scroll if # of items is enough to overflow box
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
