using Sandbox.Definitions;
using Sandbox.Game.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using VRage;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI;


namespace BDAM
{
    public partial class Session : MySessionComponentBase
    {
        internal static int Tick;

        public static bool Client;
        public static bool Server;
        public static bool MPActive;

        internal bool controlInit;
        internal static bool logging = false;
        internal static bool netlogging = false;

        private readonly Stack<GridComp> _gridCompPool = new Stack<GridComp>(128);
        private readonly ConcurrentCachingList<MyCubeGrid> _startGrids = new ConcurrentCachingList<MyCubeGrid>();
        public static ConcurrentDictionary<ulong, IMyPlayer> PlayerMap = new ConcurrentDictionary<ulong, IMyPlayer>();
        internal readonly ConcurrentDictionary<IMyCubeGrid, GridComp> GridMap = new ConcurrentDictionary<IMyCubeGrid, GridComp>();
        public static Dictionary<string, List<string>> assemblerBPs = new Dictionary<string, List<string>>();
        public static Dictionary<string, List<MyBlueprintDefinitionBase>> assemblerBP2 = new Dictionary<string, List<MyBlueprintDefinitionBase>>();

        public static Dictionary<string, List<MyBlueprintDefinitionBase>> BPClasses = new Dictionary<string, List<MyBlueprintDefinitionBase>>();
        public static Dictionary<string, MyBlueprintDefinitionBase> BPLookup = new Dictionary<string, MyBlueprintDefinitionBase>();
        public static Dictionary<string, MyBlueprintDefinitionBase> BPLookupFriendly = new Dictionary<string, MyBlueprintDefinitionBase>();
        public static Stopwatch timer = new Stopwatch();
        internal static string modName = "[BDAM]";
        internal readonly Guid storageGuid = new Guid("95dd6473-8e17-4ac3-ba22-57d283755755");
        public static float assemblerEfficiency = 1;
        internal AssemblerComp openAComp = null;
        internal static Dictionary<long, AssemblerComp> aCompMap = new Dictionary<long, AssemblerComp>();
        public static AssemblerWindow aWindow;


        //Future settings
        public static MyFixedPoint maxQueueAmount = 200; //Max amount to queue per check
        public static int refreshTime = 600; //Ticks between inventory and assembler refreshes

        private void Clean()
        {
            _gridCompPool.Clear();
            _startGrids.ClearImmediate();
            PlayerMap.Clear();
            GridMap.Clear();
            assemblerBPs.Clear();
            assemblerBP2.Clear();
            BPClasses.Clear();
            BPLookup.Clear();
            BPLookupFriendly.Clear();
            openAComp = null;
            aCompMap.Clear();
        }
    }
}
