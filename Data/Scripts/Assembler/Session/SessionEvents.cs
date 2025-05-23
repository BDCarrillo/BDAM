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
        private void GridChange(VRage.Game.ModAPI.Interfaces.IMyControllableEntity entity1, VRage.Game.ModAPI.Interfaces.IMyControllableEntity entity2)
        {
            aWindow.ToggleVisibility(openAComp, true);
            MyAPIGateway.Session.Player.Controller.ControlledEntityChanged -= GridChange;
        }
        private void OnMessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            var msg = messageText.ToLower();
            if (msg.Contains("/bdam log"))
            {
                logging = !logging;
                MyAPIGateway.Utilities.ShowNotification($"BDAM Verbose logging {(logging ? "ON" : "OFF")}");
                sendToOthers = false;
            }
            return;
        }
        private void PlayerConnected(ulong playerId)
        {
            if (netlogging)
                Log.WriteLine(modName + $"Player connected - {playerId}");
            MyAPIGateway.Multiplayer.Players.GetPlayers(null, myPlayer => FindPlayer(myPlayer, playerId));
        }
        private bool FindPlayer(IMyPlayer player, ulong id)
        {
            if (player.SteamUserId == id)
            {
                PlayerMap.TryAdd(id, player);
                if (netlogging)
                    Log.WriteLine(modName + $"Player added to map - {player.DisplayName} {id}");
            }
            return false;
        }
        private void PlayerDisco(long playerId)
        {
            if (netlogging)
                Log.WriteLine(modName + $"Player disconnected - {playerId}");
            var steamID = MyAPIGateway.Multiplayer.Players.TryGetSteamId(playerId);
            if(steamID > 0)
            {
                IMyPlayer plyr;
                PlayerMap.TryRemove(steamID, out plyr);
                foreach(var grid in GridMap.Values)
                    foreach (var aComp in grid.assemblerList.Values)
                        aComp.ReplicatedClients.Remove(steamID);
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
                Log.WriteLine($"{modName} Error with StartComps {ex}");
            }
        }
        private void OnEntityCreate(MyEntity entity)
        {
            var grid = entity as MyCubeGrid;
            if (grid != null)
            {
                _startGrids.Add(grid);
            }
            if (Client && !controlInit && entity is IMyAssembler)
            {
                controlInit = true;
                CreateTerminalControls<IMyAssembler>();
            }
        }
        public static void OpenSummary(IMyTerminalBlock block)
        {
            AssemblerComp aComp;
            var missing = "";
            var inaccessible = "";
            if(aCompMap.TryGetValue(block.EntityId, out aComp))
            {
                foreach(var missingMat in aComp.missingMatAmount)
                    missing += missingMat.Key + ": " + NumberFormat(missingMat.Value) + "\n";
                foreach (var inaccessibleComp in aComp.inaccessibleComps)
                    inaccessible += inaccessibleComp.Key + ": " + NumberFormat(inaccessibleComp.Value) + "\n";
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
            if (inaccessible.Length > 0)
            {
                d += "--Inaccessible components/materials: \n";
                d += inaccessible + "\n";
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
            if (ore.Count + ingot.Count + component.Count + ammo.Count + missing.Length + inaccessible.Length > 0)
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
                comp.Clean(true);
                _gridCompPool.Push(comp);
            }
        }
        private void ExportCustomData(IMyTerminalBlock block)
        {
            AssemblerComp aComp;
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
            {
                var output = "Item; Build amount; Grind amount; Priority";
                foreach(var item in aComp.buildList)
                    output += "\n" + item.Value.label + ";" + item.Value.buildAmount + ";" + item.Value.grindAmount + ";" + item.Value.priority;
                block.CustomData = output;
            }
        }
        private void ImportCustomData(IMyTerminalBlock block)
        {
            AssemblerComp aComp;
            if (aCompMap.TryGetValue(block.EntityId, out aComp))
            {
                string[] import = block.CustomData.Split('\n');
                bool changesMade = false;
                MyAPIGateway.Utilities.ShowMessage("BDAM", $"Importing {import.Length - 1} lines");
                for (int i = 1; i <= import.Length - 1; i++)
                {
                    string[] input = import[i].Split(';');
                    string errorMsg = "";
                    int buildNum = 0;
                    int grindNum = 0;
                    int priorityNum = 0;
                    bool buildNumValid = false;
                    bool grindNumValid = false;
                    bool priorityNumValid = false;
                    bool bpBaseValid = false;
                    MyBlueprintDefinitionBase bpBase = null;

                    //error checking
                    if (input.Length == 4)
                    {
                        bpBaseValid = BPLookupFriendly.TryGetValue(input[0], out bpBase);
                        buildNumValid = int.TryParse(input[1], out buildNum);
                        grindNumValid = int.TryParse(input[2], out grindNum);
                        priorityNumValid = int.TryParse(input[3], out priorityNum) && priorityNum > 0 && priorityNum < 4;
                    }
                    else
                        errorMsg += " missing data element(s)";


                    if (buildNumValid && grindNumValid && priorityNumValid && bpBaseValid)
                    {
                        var name = input[0];
                        if (aComp.buildList.ContainsKey(bpBase))
                        {
                            var existinglComp = aComp.buildList[bpBase];
                            //Already in queue and no changes
                            if (existinglComp.grindAmount == grindNum && existinglComp.priority == priorityNum && existinglComp.buildAmount == buildNum)
                                continue;
                            else //Already in queue but modified
                            {
                                existinglComp.grindAmount = grindNum;
                                existinglComp.priority = priorityNum;
                                existinglComp.buildAmount = buildNum;
                                existinglComp.dirty = true;
                                changesMade = true;
                                MyAPIGateway.Utilities.ShowMessage("BDAM", $"Updated {existinglComp.label}");
                                continue;
                            }
                        }
                        //Else new to queue
                        var importlComp = new ListCompItem() { buildAmount = buildNum, grindAmount = grindNum, priority = priorityNum, label = name, bpBase = bpBase.Id.SubtypeName, dirty = true };
                        aComp.buildList.Add(bpBase, importlComp);
                        changesMade = true;
                        MyAPIGateway.Utilities.ShowMessage("BDAM", $"Added {name}");
                    }
                    else
                    {
                        if (errorMsg.Length == 0)
                        {
                            if (!bpBaseValid) errorMsg += " name does not match a BP";
                            if (!buildNumValid) errorMsg += " build amount invalid";
                            if (!grindNumValid) errorMsg += " grind amount invalid";
                            if (!priorityNumValid) errorMsg += " priority invalid";
                        }
                        MyAPIGateway.Utilities.ShowMessage("BDAM", $"Line {i} error{errorMsg}");
                    }
                }

                if (changesMade)
                    aComp.Save();
            }
        }
    }
}
