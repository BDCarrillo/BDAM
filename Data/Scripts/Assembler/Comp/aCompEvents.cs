using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI.Ingame;
using VRage.Utils;

namespace BDAM
{
    public partial class AssemblerComp
    {
        private void Assembler_StartedProducing()
        {
            if (runStartTick == Session.Tick)
                return;
            countStart++;
            inputJammed = false;
            outputJammed = false;
            unJamAttempts = 0;
            if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + " started production ");
            runStartTick = Session.Tick; //Dumb... action runs twice/assembler
        }
        private void Assembler_StoppedProducing()
        {
            if (runStopTick == Session.Tick)
                return;
            countStop++;
            if (Session.logging && assembler.IsQueueEmpty) Log.WriteLine(Session.modName + assembler.CustomName + " finished queue");
            //Production stopped
            if (assembler.IsFunctional && assembler.Enabled && !assembler.IsQueueEmpty)
            {
                if (assembler.InputInventory.VolumeFillFactor > 0.95f) //Input jammed up (less than 5% space remaining)
                {
                    if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + " Input jammed" + assembler.InputInventory.VolumeFillFactor * 100 + " % full");
                    inputJammed = true;
                }

                if (assembler.OutputInventory.VolumeFillFactor > 0.95f) //Output jammed up (less than 5% space remaining)
                {
                    if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + " Output jammed" + assembler.OutputInventory.VolumeFillFactor * 100 + " % full");
                    outputJammed = true;
                }

                if(!(inputJammed || outputJammed)) //Missing materials.  Will be eval'd on next update, as it may still pull resources
                {
                    lastQueue = assembler.GetQueue()[0];
                    if (Session.logging) Log.WriteLine(Session.modName + assembler.CustomName + " stopped - missing materials - Keen attempting pull");
                }
            }
            runStopTick = Session.Tick; //Dumb... action runs twice/assembler
        }
        public void UnJamAssembler(GridComp gComp, AssemblerComp aComp)
        {
            //TODO: Look at ejecting items not needed by the current recipe
            var aCube = aComp.assembler as MyCubeBlock;
            var aInput = aComp.assembler.InputInventory;

            MyFixedPoint max = 0;
            MyInventoryItem? largestStack = null;
            for (int i = 0; i < aInput.ItemCount - 1; i++)
            {
                var curItem = aInput.GetItemAt(i);
                if (curItem == null)
                {
                    MyLog.Default.Error(Session.modName + assembler.CustomName + " Current item was null in unjam attempt");
                    continue;
                }
                if (curItem.Value.Amount > max)
                {
                    max = curItem.Value.Amount;
                    largestStack = (MyInventoryItem)curItem;
                }
            }

            if (largestStack != null)
            {
                var transferAmount = MyFixedPoint.MultiplySafe(largestStack.Value.Amount, 0.5f);
                foreach (var block in gComp.Grid.Inventories)
                {
                    if (block == aCube || !(block is IMyCargoContainer))
                        continue;

                    if (aInput.TransferItemTo(block.GetInventory(), largestStack.Value, transferAmount))
                        break;
                }
            }
            else
            {
                MyLog.Default.Error(Session.modName + assembler.CustomName + " Largest stack was null in unjam attempt - cancelling unjam attempts");
                aComp.unJamAttempts = 4;
            }

            aComp.unJamAttempts++;
            gComp.countUnjamAttempts++;
        }
    }
}
