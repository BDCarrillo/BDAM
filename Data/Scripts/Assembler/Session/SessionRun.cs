using RichHudFramework.UI.Client;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;
using RichHudFramework.Client;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.Game;
using System.Reflection;

namespace BDAM
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        public override void LoadData()
        {
            MPActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            Server = (MPActive && MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            Client = (MPActive && !MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            if (Server)
                MyEntities.OnEntityCreate += OnEntityCreate;
            if(Client)
            {
                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;

                if (logging)
                    MyLog.Default.WriteLineAndConsole(modName + "Registered client controls");
            }
        }

        public override void BeforeStart()
        {
            if(Client)
                AssemblerHud.Init();

            //Find assemblers and BP classes they can make
            foreach (var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                if(def is MyAssemblerDefinition)
                {
                    //Iterate BP classes an assembler can build
                    var aDef = def as MyAssemblerDefinition;
                    var bpClassSubtypeNames = new List<string>();

                    foreach(var bpClass in aDef.BlueprintClasses)
                    {
                        if (bpClass.Id.SubtypeName == "LargeBlocks" || bpClass.Id.SubtypeName == "SmallBlocks" || bpClass.Id.SubtypeName == "BuildPlanner")
                            continue;

                        bpClassSubtypeNames.Add(bpClass.Id.SubtypeName); //Add BP class to assembler list

                        if (BPClasses.ContainsKey(bpClass.Id.SubtypeName))
                            continue;

                        var bpList = new List<MyBlueprintDefinitionBase>();
                        foreach (var bp in bpClass)
                        {
                            bpList.Add(bp);
                            if(!BPLookup.ContainsKey(bp.Id.SubtypeName))
                                BPLookup.Add(bp.Id.SubtypeName, bp);
                        }

                        BPClasses.Add(bpClass.Id.SubtypeName, bpList);                           
                    }
                    assemblerBPs.Add(def.Id.SubtypeId.ToString(), bpClassSubtypeNames);//Pop assembler specific list
                }
            }
            foreach(var a in assemblerBPs)
            {
                var assemblerName = a.Key;
                var assemblerBPList = a.Value;
                assemblerBP2.Add(assemblerName, new List<MyBlueprintDefinitionBase>());
                foreach (var bpClass in assemblerBPList)
                    foreach (var bp in BPClasses[bpClass])
                        assemblerBP2[assemblerName].Add(bp);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (!_startGrids.IsEmpty && Tick % 30 == 0)
                StartComps();

            for (int i = 0; i < GridList.Count; i++)
            {
                var gridComp = GridList[i];
                if (gridComp.assemblerList.Count > 0 && gridComp.nextUpdate <= Tick) //TODO look at dampening client updates to their own/faction owned grids
                {
                    if (Server)//TODO look at sending updates/notifications to players on unjam failures/states
                    {
                        gridComp.UpdateGrid();
                    }
                }
            }
            Tick++;
        }
        protected override void UnloadData()
        {
            if (Server)
            {
                foreach (var gridComp in GridList)
                {
                    gridComp.Clean();
                }
                MyEntities.OnEntityCreate -= OnEntityCreate;
                Clean();
            }
            if (Client)
            {
                MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;
            }

        }
    }
}
