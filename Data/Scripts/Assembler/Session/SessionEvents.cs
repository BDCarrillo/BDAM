using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace BDAM
{
    public partial class Session : MySessionComponentBase
    {
        private void PlayerDisco(long playerId)
        {
            if (netlogging)
                MyLog.Default.WriteLineAndConsole(modName + $"Player disconnected - {playerId}");
            var steamID = MyAPIGateway.Multiplayer.Players.TryGetSteamId(playerId);
            if(steamID > 0)
            {
                foreach(var grid in GridMap.Values)
                {
                    foreach (var aComp in grid.assemblerList.Values)
                        aComp.ReplicatedClients.Remove(steamID);
                }
            }
        }
        private void CombineItemStacks(IMyTerminalBlock block)
        {
            timer.Restart();
            var controlledGrid = (MyCubeGrid)block.CubeGrid;
            MyInventoryBase inventory;
            int stacksCombined = 0;
            int containersChecked = 0;
            foreach (MyCubeBlock cargo in controlledGrid.GetFatBlocks())
            {
                if (!(cargo is IMyCargoContainer))
                    continue;
                containersChecked++;
                if (cargo.TryGetInventory(out inventory))
                {
                    var myInv = inventory as IMyInventory;
                    var invList = inventory.GetItems();
                    var checkedItems = new List<MyStringHash>();
                    for (int i = 0; i < myInv.ItemCount; i++)
                    {
                        var currItem = invList[i];
                        if (checkedItems.Contains(currItem.Content.SubtypeId))
                            continue;
                        else
                            checkedItems.Add(currItem.Content.SubtypeId);
                        for (int j = i + 1; j < myInv.ItemCount; j++)
                        {
                            var checkItem = invList[j];
                            if (currItem.Content.SubtypeId == checkItem.Content.SubtypeId)
                            {
                                myInv.TransferItemTo(myInv, j, i, true);
                                stacksCombined++;
                            }
                        }
                    }
                }
            }
            timer.Stop();
            var d = $"{controlledGrid.DisplayName} \n \n Cargo Containers Checked: {containersChecked} \n Stacks Combined: {stacksCombined}" +
                $"\n\n Runtime: {timer.Elapsed.TotalMilliseconds} ms";
            MyAPIGateway.Utilities.ShowMissionScreen("Cargo Container Stacking Complete", "", "", d, null, "Close");
        }
        internal void StartComps()
        {
            try
            {
                _startGrids.ApplyAdditions();
                for (int i = 0; i < _startGrids.Count; i++)
                {
                    var grid = _startGrids[i];
                    if (grid.IsPreview)
                        continue;

                    var gridComp = _gridCompPool.Count > 0 ? _gridCompPool.Pop() : new GridComp();
                    gridComp.Init(grid, this);

                    GridMap[grid] = gridComp;
                    grid.OnClose += OnGridClose;
                }
                _startGrids.ClearImmediate();
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole($"{modName} Error with StartComps {ex}");
            }
        }
        private void OnEntityCreate(MyEntity entity)
        {
            var grid = entity as MyCubeGrid;
            if (grid != null)
            {
                _startGrids.Add(grid);
            }
            if (Client && !controlInit)
            {
                var assembler = entity as IMyAssembler;
                if (assembler != null)
                {
                    CreateTerminalControls<IMyAssembler>();
                    controlInit = true;
                }
            }
        }
        public static void OpenSummary(IMyTerminalBlock block)
        {
            AssemblerComp aComp;
            var missing = "";
            if(aCompMap.TryGetValue(block.EntityId, out aComp))
            {
                foreach(var missingMat in aComp.missingMatAmount)
                {
                    missing += missingMat.Key + ": " + NumberFormat(missingMat.Value) + "\n";
                }
            }

            timer.Restart();
            var controlledGrid = (MyCubeGrid)block.CubeGrid;
            string d = "";
            int padLen = 0;
            Dictionary<string, float> ore = new Dictionary<string, float>();
            Dictionary<string, float> ingot = new Dictionary<string, float>();
            Dictionary<string, float> component = new Dictionary<string, float>();
            Dictionary<string, float> ammo = new Dictionary<string, float>();

            MyInventoryBase inventory;
            foreach (var b in controlledGrid.Inventories)
            {
                if (b.TryGetInventory(out inventory))
                {
                    var invList = inventory.GetItems();
                    foreach (MyPhysicalInventoryItem item in invList)
                    {
                        var itemType = item.Content.TypeId.ToString();
                        var itemName = item.Content.SubtypeName;

                        switch (itemType)
                        {
                            case "MyObjectBuilder_Ore":
                                {
                                    if (!ore.ContainsKey(itemName))
                                        ore.Add(itemName, 0);
                                    ore[itemName] += (float)item.Amount;
                                }
                                break;
                            case "MyObjectBuilder_Ingot":
                                {
                                    if (itemName == "Stone") itemName = "Gravel"; //Thx Keen

                                    if (!ingot.ContainsKey(itemName))
                                        ingot.Add(itemName, 0);
                                    ingot[itemName] += (float)item.Amount;
                                }
                                break;
                            case "MyObjectBuilder_Component":
                                {
                                    if (!component.ContainsKey(itemName))
                                        component.Add(itemName, 0);
                                    component[itemName] += (float)item.Amount;
                                }
                                break;
                            case "MyObjectBuilder_AmmoMagazine":
                                {
                                    var ammoID = new MyDefinitionId(item.Content.TypeId, item.Content.SubtypeId);
                                    var MagazineDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(ammoID);
                                    itemName = MagazineDef.DisplayNameText;
                                    if (!ammo.ContainsKey(itemName))
                                        ammo.Add(itemName, 0);
                                    ammo[itemName] += (float)item.Amount;
                                }
                                break;
                        }
                        if (itemName.Length > padLen)
                        {
                            padLen = itemName.Length + 4;
                        }
                    }
                }
            }
            if (missing.Length > 0)
            {
                d += "--Missing/Insufficient materials: \n";
                d += missing + "\n";
            }


            if (ammo.Count > 0)
                d += "--Ammo: \n";
            foreach (var x in ammo)
                d += x.Key.PadRight(padLen + 15, '.') + NumberFormat(x.Value) + "\n";
            if (ore.Count > 0)
                d += "\n--Ore: \n";
            foreach (var x in ore)
                d += x.Key.PadRight(padLen + 15, '.') + NumberFormat(x.Value) + "\n";
            if (ingot.Count > 0)
                d += "\n --Ingots: \n";
            foreach (var x in ingot)
                d += x.Key.PadRight(padLen + 15, '.') + NumberFormat(x.Value) + "\n";
            if (component.Count > 0)
                d += "\n--Components: \n";
            foreach (var x in component)
                d += x.Key.PadRight(padLen + 15, '.') + NumberFormat(x.Value) + "\n";

            timer.Stop();
            d += $"\n\n Runtime: {timer.Elapsed.TotalMilliseconds} ms";
            if (ore.Count + ingot.Count + component.Count + ammo.Count + missing.Length > 0)
                MyAPIGateway.Utilities.ShowMissionScreen("Inventory Summary", "", "", d, null, "Close");
            else
                MyAPIGateway.Utilities.ShowNotification("No ore, ingots, components, missing items, or ammo found");
        }
        private void OnGridClose(IMyEntity entity)
        {
            var grid = entity as MyCubeGrid;
            GridComp comp;
            if (GridMap.TryRemove(grid, out comp))
            {
                comp.Clean();
                _gridCompPool.Push(comp);
            }
        }
    }
}
