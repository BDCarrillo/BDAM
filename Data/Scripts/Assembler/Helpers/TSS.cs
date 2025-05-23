using System;
using Sandbox.Game.GameSystems.TextSurfaceScripts;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using BDAM;
using Sandbox.Game.Entities;
using System.Reflection;
using Sandbox.Game.Replication.StateGroups;

namespace BDAMTSS
{
    [MyTextSurfaceScript("BDAMTSS", "BDAM Info")]
    public class BDAMTSS : MyTSSCommon
    {
        private readonly IMyTerminalBlock TerminalBlock;
        private MyIni config = new MyIni();
        private float textSize = 1.0f;
        private long assemblerID = 0;
        private string cachedName = "";
        private Vector2 newLine;
        private IMyTextSurface mySurface;
        public override ScriptUpdate NeedsUpdate => ScriptUpdate.Update100;
        public BDAMTSS(IMyTextSurface surface, IMyCubeBlock block, Vector2 size) : base(surface, block, size)
        {
            mySurface = surface;
            TerminalBlock = (IMyTerminalBlock)block;
            TerminalBlock.OnMarkForClose += BlockMarkedForClose;
        }

        public override void Dispose()
        {
            base.Dispose();
            TerminalBlock.OnMarkForClose -= BlockMarkedForClose;
        }

        void BlockMarkedForClose(IMyEntity ent)
        {
            Dispose();
        }

        public override void Run()
        {
            try
            {
                base.Run(); // do not remove

                if (TerminalBlock.CustomData.Length <= 0)
                    CreateConfig();

                if (!LoadConfig())
                {
                    Notification("CFG ERROR, CUSTOM DATA RESET");
                    CreateConfig();
                    return;
                }
                var name = config.Get("Settings", "Assembler").ToString();

                if (name == "Enter Name Here")
                {
                    Notification("ENTER NAME IN CUSTOM DATA");
                    return;
                }
                else if (name != cachedName)
                    FindAssembler(name);

                if (assemblerID == 0)
                {
                    Notification("ASSEMBLER NOT FOUND");
                    return;
                }
                AssemblerComp aComp;
                if (Session.aCompMap.TryGetValue(assemblerID, out aComp))
                    Draw(aComp);
                else
                {
                    Notification("ASSEMBLER COMP NOT FOUND");
                    return;
                }
            }
            catch (Exception e)
            {
                DrawError(e);
            }
        }

        private void Draw(AssemblerComp aComp)
        {
            Vector2 screenSize = Surface.SurfaceSize;
            Vector2 screenCorner = (Surface.TextureSize - screenSize) * 0.5f;

            textSize = config.Get("Settings", "TextSize").ToSingle(defaultValue: 1.0f);
            newLine = new Vector2(0, 30 * textSize);

            var frame = Surface.DrawFrame();
            var myViewport = new RectangleF((mySurface.TextureSize - mySurface.SurfaceSize) / 2f, mySurface.SurfaceSize);
            var myPosition = new Vector2(5, 5 + (30 * textSize * 2)) + myViewport.Position;
            var startPos = myPosition;
            var titlePos = new Vector2(mySurface.SurfaceSize.X / 2, 5);
            WriteTextSprite(ref frame, "[" + cachedName.ToUpper() + "]", titlePos, TextAlignment.CENTER);
            titlePos += newLine;
            WriteTextSprite(ref frame, $"{(aComp.autoControl ? "Auto: On" : "Auto: Off")}  {(aComp.masterMode ? "Master" : "")}", titlePos, TextAlignment.CENTER);

            if (aComp.missingMatAmount.Count > 0)
            {
                WriteTextSprite(ref frame, "Missing/Insufficient Materials:", myPosition, TextAlignment.LEFT);
                myPosition += newLine;
                foreach (var missing in aComp.missingMatAmount)
                {
                    var label = missing.Key == "Stone" ? "Gravel" : missing.Key;
                    WriteTextSprite(ref frame, "  " + label + ": " + Session.NumberFormat(missing.Value), myPosition, TextAlignment.LEFT);
                    myPosition += newLine;
                }
            }

            if (aComp.inaccessibleComps.Count > 0)
            {
                WriteTextSprite(ref frame, "Inaccessible Items/Comps:", myPosition, TextAlignment.LEFT);
                myPosition += newLine;
                foreach (var inaccessible in aComp.inaccessibleComps)
                {
                    var label = inaccessible.Key == "Stone" ? "Gravel" : inaccessible.Key;
                    WriteTextSprite(ref frame, "  " + label + ": " + Session.NumberFormat(inaccessible.Value), myPosition, TextAlignment.LEFT);
                    myPosition += newLine;
                }
            }

            if (myPosition == startPos)
            {
                var msg = "[NOMINAL]";
                if (aComp.helperMode)
                    msg = "[HELPER MODE, CHECK MASTER]";
                else if (!aComp.autoControl)
                    msg = "[AUTO MODE OFF]";
                else if (aComp.buildList.Count == 0)
                    msg = "[NOTHING IN QUEUE]";
                titlePos += newLine + newLine;
                WriteTextSprite(ref frame, msg, titlePos, TextAlignment.CENTER);
            }
            frame.Dispose();
        }

        void WriteTextSprite(ref MySpriteDrawFrame frame, string text, Vector2 position, TextAlignment alignment)
        {
            var sprite = new MySprite
            {
                Type = SpriteType.TEXT,
                Data = text,
                Position = position,
                RotationOrScale = textSize,
                Color = mySurface.ScriptForegroundColor,
                Alignment = alignment,
                FontId = "White"
            };

            frame.Add(sprite);
        }

        private void FindAssembler(string name)
        {
            assemblerID = 0;
            var grid = TerminalBlock.CubeGrid as MyCubeGrid;
            foreach (var block in grid.GetFatBlocks())
            {
                if (!(block is IMyAssembler))
                    continue;
                var assy = block as IMyAssembler;
                if (assy.CustomName == name)
                    assemblerID = assy.EntityId;
            }
            cachedName = name;
        }

        private void CreateConfig()
        {
            config.Clear();
            config.AddSection("Settings");
            config.Set("Settings", "TextSize", "1.0");
            config.Set("Settings", "Assembler", "Enter Name Here");

            config.Invalidate();
            TerminalBlock.CustomData = config.ToString();
        }

        private bool LoadConfig()
        {
            if (config.TryParse(TerminalBlock.CustomData) && config.ContainsSection("Settings"))
                return true;
            return false;
        }

        void Notification(string msg)
        {
            Vector2 screenSize = Surface.SurfaceSize;
            Vector2 screenCorner = (Surface.TextureSize - screenSize) * 0.5f;

            var frame = Surface.DrawFrame();

            var bg = new MySprite(SpriteType.TEXTURE, "SquareSimple", null, null, Color.Black);
            frame.Add(bg);

            var text = MySprite.CreateText(msg + "\n" + cachedName, "White", Color.Red, 1f, TextAlignment.LEFT);
            text.Position = screenCorner + new Vector2(16, 16);
            frame.Add(text);

            frame.Dispose();
        }


        void DrawError(Exception e)
        {
            MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");
            try 
            {
                Vector2 screenSize = Surface.SurfaceSize;
                Vector2 screenCorner = (Surface.TextureSize - screenSize) * 0.5f;

                var frame = Surface.DrawFrame();

                var bg = new MySprite(SpriteType.TEXTURE, "SquareSimple", null, null, Color.Black);
                frame.Add(bg);

                var text = MySprite.CreateText($"ERROR: {e.Message}\n{e.StackTrace}\n\nPlease send screenshot of this to mod author.\n{MyAPIGateway.Utilities.GamePaths.ModScopeName}", "White", Color.Red, 0.7f, TextAlignment.LEFT);
                text.Position = screenCorner + new Vector2(16, 16);
                frame.Add(text);

                frame.Dispose();
            }
            catch (Exception e2)
            {
                MyLog.Default.WriteLineAndConsole($"Also failed to draw error on screen: {e2.Message}\n{e2.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }
    }
}