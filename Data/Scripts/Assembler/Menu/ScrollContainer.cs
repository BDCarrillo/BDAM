﻿using System;
using VRageMath;
using RichHudFramework.UI;
using Sandbox.ModAPI;
using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.Utils;

namespace BDAM
{
    public class WindowScrollContainer : HudElementBase
    {
        public readonly ScrollBox addMulti;
        public readonly LabelBox title;
        public readonly TextBox infoPanel;
        private readonly BorderedButton close, add, clearAll, addAll, autoMode, summary, notify, maxQueue;
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
                Height = 60f * Session.resMult,
                Color = new Color(41, 54, 62),
                Padding = new Vector2(10,0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1.5f * Session.resMult, RichHudFramework.UI.Rendering.FontStyles.Underline),
                VertCenterText = false,
                AutoResize = false,
                ZOffset = 2,
            };
            //Labels
            new TextBox(title)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Left,
                Width = 300 * Session.resMult,
                Height = title.Height * 0.5f,
                Offset = new Vector2(10, -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1.25f * Session.resMult),
                AutoResize = false,
                Text = "Item Name",
                InputEnabled = false,
            };
            new TextBox(title)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Left,
                Width = 300 * Session.resMult,
                Height = title.Height * 0.5f,
                Offset = new Vector2(325 * Session.resMult, -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1.25f * Session.resMult),
                AutoResize = false,
                Text = "Build to Qty",
                InputEnabled = false,
            };
            new TextBox(title)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Left,
                Width = 300 * Session.resMult,
                Height = title.Height * 0.5f,
                Offset = new Vector2(465 * Session.resMult, -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1.25f * Session.resMult),
                AutoResize = false,
                Text = "Grind to Qty",
                InputEnabled = false,
            };

            //Corner X
            close = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = title.Height * 0.5f,
                Offset = new Vector2(10, -10),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 2f * Session.resMult),
                AutoResize = false,
                Width = 100 * Session.resMult,
                Text = "X",
                ZOffset = 50,
                TextPadding = new Vector2(8 * Session.resMult, 8 * Session.resMult),
                UseFocusFormatting = false,
            };
            close.background.Width = close.Width;

            //Buttons: Top row
            summary = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = title.Height * 0.5f,
                Offset = new Vector2(close.Offset.X - close.Width - (20 * Session.resMult), 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f * Session.resMult),
                AutoResize = false,
                Width = 140 * Session.resMult,
                Text = "Summary",
                ZOffset = 50,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            summary.background.Width = summary.Width;
            summary.background.Padding = Vector2.Zero;

            notify = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = title.Height * 0.5f,
                Offset = new Vector2(summary.Offset.X - summary.Width, 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 0.9f * Session.resMult),
                AutoResize = false,
                Width = 140 * Session.resMult,
                Text = "Msg: ---",
                ZOffset = 50,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            notify.background.Width = notify.Width;
            notify.background.Padding = Vector2.Zero;

            autoMode = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = title.Height * 0.5f,
                Offset = new Vector2(notify.Offset.X - notify.Width, 0),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f * Session.resMult),
                AutoResize = false,
                Width = 140 * Session.resMult,
                Text = "Auto: ---",
                ZOffset = 50,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            autoMode.background.Width = autoMode.Width;
            autoMode.background.Padding = Vector2.Zero;

            //Buttons: Bottom row
            clearAll = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = title.Height * 0.5f,
                Offset = new Vector2(close.Offset.X - close.Width - (20 * Session.resMult), -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f * Session.resMult),
                AutoResize = false,
                Width = 140 * Session.resMult,
                Text = "Clear All",
                ZOffset = 50,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            clearAll.background.Width = clearAll.Width;
            clearAll.background.Padding = Vector2.Zero;

            addAll = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = title.Height * 0.5f,
                Offset = new Vector2(clearAll.Offset.X - clearAll.Width, -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f * Session.resMult),
                AutoResize = false,
                Width = 140 * Session.resMult,
                Text = "Add All",
                ZOffset = 50,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            addAll.background.Width = addAll.Width;
            addAll.background.Padding = Vector2.Zero;

            add = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = title.Height * 0.5f,
                Offset = new Vector2(addAll.Offset.X - addAll.Width, -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Center, 1f * Session.resMult),
                AutoResize = false,
                Width = 140 * Session.resMult,
                Text = "Add",
                ZOffset = 50,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            add.background.Width = add.Width;
            add.background.Padding = Vector2.Zero;

            maxQueue = new BorderedButton(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Height = title.Height * 0.5f,
                Offset = new Vector2(add.Offset.X - add.Width, -title.Height * 0.5f),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1f * Session.resMult),
                AutoResize = false,
                Width = 220 * Session.resMult,
                Text = "Max Queue: ---",
                ZOffset = 50,
                UseFocusFormatting = false,
                TextPadding = new Vector2(8, 0),
                Padding = new Vector2(8, 0),
            };
            maxQueue.background.Width = maxQueue.Width;
            maxQueue.background.Padding = Vector2.Zero;

            //Info panel
            infoPanel = new TextBox(this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Width = 300 * Session.resMult,
                Height = (700 - title.Height) * Session.resMult,
                Offset = new Vector2(0, -title.Height),
                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 1.25f * Session.resMult),
                AutoResize = false,
                VertCenterText = false,
                InputEnabled = false,
                BuilderMode = TextBuilderModes.Lined,
                ZOffset = 0,
            };

            addMulti = new ScrollBox(true, this)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Right,
                Width = 300 * Session.resMult,
                Height = (700 - title.Height) * Session.resMult,
                Offset = new Vector2(0, -title.Height),
                ZOffset = 1,
                InputEnabled = false,
                Visible = false,
            };
            addMulti.MinLength = 10;

            //Controls
            notify.MouseInput.LeftClicked += LeftClick;
            summary.MouseInput.LeftClicked += LeftClick;
            autoMode.MouseInput.LeftClicked += LeftClick;
            addAll.MouseInput.LeftClicked += LeftClick;
            clearAll.MouseInput.LeftClicked += LeftClick;
            add.MouseInput.LeftClicked += LeftClick;
            close.MouseInput.LeftClicked += LeftClick;
            maxQueue.MouseInput.LeftClicked += QueueLeftClicked;
            maxQueue.MouseInput.RightClicked += QueueRightClicked;
        }
        private void UpdateAddMulti()
        {
            while (addMulti.Count > 0)
            {
                //addMulti.RemoveChild(addMulti[0].Element);
                //addMulti[0].Element.Unregister();
                addMulti.RemoveAt(0);
            }

            var tempDict = new Dictionary<string, MyBlueprintDefinitionBase>();
            var sortList = new List<string>();
            foreach (var bp in Session.assemblerBP2[aComp.assembler.BlockDefinition.SubtypeId])
            {
                if (!aComp.buildList.ContainsKey(bp) && !sortList.Contains(bp.Results[0].Id.SubtypeName))
                {
                    tempDict[bp.Results[0].Id.SubtypeName] = bp;
                    sortList.Add(bp.Results[0].Id.SubtypeName);
                }
            }
            sortList.Sort();

            foreach (var bp in sortList)
            {
                addMulti.Add(new AddItem(tempDict[bp], null));
            }
        }

        private void LeftClick(object sender, EventArgs e)
        {
            if (sender == close)
            {
                if(addMulti.Visible)
                {
                    CycleInputMasking(true);
                    UpdateAddMulti();
                }
                else
                    Session.aWindow.ToggleVisibility(aComp);
            }
            else if (sender == addAll)
            {
                aComp.tempRemovalList.Clear();
                foreach (var bp in Session.assemblerBP2[aComp.assembler.BlockDefinition.SubtypeId])
                {
                    if (!aComp.buildList.ContainsKey(bp))
                    {
                        var tempListCompItem = new ListCompItem() { bpBase = bp.Id.SubtypeName, label = bp.Results[0].Id.SubtypeName, dirty = true };
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
                if (aComp.assembler.CooperativeMode)
                {
                    MyAPIGateway.Utilities.ShowNotification("Disable co-operative mode to utilize automatic control", font: "Red");
                    return;
                }
                aComp.autoControl = !aComp.autoControl;
                autoMode.Text = "Auto: " + (aComp.autoControl ? "On" : "Off");
                if (Session.MPActive)
                {
                    if (Session.netlogging)
                        Log.WriteLine(Session.modName + $"Sending updated auto control state to server " + aComp.autoControl);
                    var packet = new UpdateStatePacket { Var = UpdateType.autoControl, Value = aComp.autoControl ? 1 : 0, Type = PacketType.UpdateState, EntityId = aComp.assembler.EntityId };
                    Session.SendPacketToServer(packet);
                }
            }
            else if (sender == notify)
            {
                aComp.notification++;
                if (aComp.notification > 2)
                    aComp.notification = 0;
                notify.Text = "Msg: " + (aComp.notification == 0 ? "Own" : aComp.notification == 1 ? "Fac" :"Off");
                if (Session.MPActive)
                {
                    if (Session.netlogging)
                        Log.WriteLine(Session.modName + $"Sending updated notification state to server " + aComp.notification);
                    var packet = new UpdateStatePacket { Var = UpdateType.notification, Value = aComp.notification, Type = PacketType.UpdateState, EntityId = aComp.assembler.EntityId };
                    Session.SendPacketToServer(packet);
                }
            }
            else if (sender == summary)
            {
                Session.OpenSummary(aComp.assembler);
            }
            else if (sender == add)
            {
                if (addMulti.Visible)
                {
                    bool needUpdate = false;
                    foreach (var addItem in addMulti)
                    {
                        var details = addItem.Element as AddItem;
                        if (details.boxChecked)
                        {
                            var bp = details.bp;
                            var tempListCompItem = new ListCompItem() { bpBase = bp.Id.SubtypeName, label = bp.Results[0].Id.SubtypeName, dirty = true };
                            aComp.buildList.Add(bp, tempListCompItem);
                            if (aComp.tempRemovalList.Contains(bp.Id.SubtypeName))
                                aComp.tempRemovalList.Remove(bp.Id.SubtypeName);
                            needUpdate = true;
                        }
                    }
                    CycleInputMasking(true);
                    if (needUpdate) Update(true);
                }
                else
                {
                    //Check if all possible BPs already in list (IE add all clicked)
                    if (aComp.buildList.Count < Session.assemblerBP2[aComp.assembler.BlockDefinition.SubtypeId].Count)
                        CycleInputMasking(false);
                    else
                        MyAPIGateway.Utilities.ShowNotification("All possible items already added", font: "Red");
                }
            }
        }

        private void QueueRightClicked(object sender, EventArgs e)
        {
            UpdateQty(false);
        }
        private void QueueLeftClicked(object sender, EventArgs e)
        {
            UpdateQty(true);
        }
        private void UpdateQty(bool increase)
        {
            var original = aComp.maxQueueAmount;
            var shift = MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.Shift); //100
            var control = MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.Control); //10
            var both = shift && control;

            var amount = both ? 1000 : shift ? 100 : control ? 10 : 1;
            if (!increase)
                amount *= -1;
            aComp.maxQueueAmount += amount;
            if (aComp.maxQueueAmount < 1)
                aComp.maxQueueAmount = 1;
            maxQueue.Text = "Max Queue: " + Session.NumberFormat(aComp.maxQueueAmount);
            if (original != aComp.maxQueueAmount)
                aComp.queueDirty = true;
        }
        private void CycleInputMasking(bool enable)
        {
            foreach (var item in queueList)
                item.InputEnabled = enable;
            autoMode.InputEnabled = enable;
            addAll.InputEnabled = enable;
            clearAll.InputEnabled = enable;
            summary.InputEnabled = enable;

            addMulti.Visible = !enable;
            addMulti.InputEnabled = !enable;
            infoPanel.Visible = !addMulti.Visible;
        }

        public void RemoveQueueItem(MyBlueprintDefinitionBase key)
        {
            aComp.buildList.Remove(key);
            if(!aComp.tempRemovalList.Contains(key.Id.SubtypeName))
                aComp.tempRemovalList.Add(key.Id.SubtypeName);
            Update(true);
        }

        public void Update(bool rebuild = false)
        {
            if (rebuild)
            {
                startPos = 0;
                notify.Text = "Msg: " + (aComp.notification == 0 ? "Own" : aComp.notification == 1 ? "Fac" : "Off");
                autoMode.Text = "Auto: " + (aComp.autoControl ? "On" : "Off");
                maxQueue.Text = "Max Queue: " + Session.NumberFormat(aComp.maxQueueAmount);
                Clear();

                //Buildlist alphabetical sorting
                var sortedList = new List<string>();
                var refDict = new Dictionary<string, QueueItem>();
                foreach (var item in aComp.buildList)
                {
                    if (sortedList.Contains(item.Value.label))
                    {
                        var errorMsg = $"BDAM error, multiple BPs for {item.Value.label}";
                        Log.WriteLine(errorMsg);
                        MyLog.Default.WriteLineAndConsole(errorMsg);
                        MyAPIGateway.Utilities.ShowNotification(errorMsg, 2000, "Red");
                    }
                    else
                    {
                        sortedList.Add(item.Value.label);
                        refDict[item.Value.label] = new QueueItem(item.Value, item.Key, this);
                    }
                }
                sortedList.Sort();

                foreach (var item in sortedList)
                {
                    queueList.Add(refDict[item]);
                }
            }
            
            //Starting offset to get scrollbox list items below header bar
            float offset = (60 * Session.resMult + 5) * -1;

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
            UpdateAddMulti();

            string infoString = "";
            if (aComp.missingMatAmount.Count > 0)
            {
                infoString += "Missing/Insufficient materials:\n";
                foreach (var missing in aComp.missingMatAmount)
                    infoString += "  " + missing.Key + ": " + Session.NumberFormat(missing.Value) + "\n";
            }
            if (aComp.inaccessibleItems.Count > 0)
            {
                if (infoString.Length > 0)
                    infoString += "\n";
                infoString += "Inaccessible items:\n";
                foreach (var inaccessible in aComp.inaccessibleItems)
                    infoString += "  " + inaccessible.Key + ": " + Session.NumberFormat(inaccessible.Value) + "\n";
            }
            infoPanel.Text = infoString;
        }

        private void Clear(bool delete = false)
        {
            if(delete)
            {
                foreach (var item in aComp.buildList)
                {
                    if (!aComp.tempRemovalList.Contains(item.Key.Id.SubtypeName))
                        aComp.tempRemovalList.Add(item.Key.Id.SubtypeName);
                }
                aComp.buildList.Clear();
                infoPanel.Text = "";
                UpdateAddMulti();
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
