using System.Collections.Generic;
using ProtoBuf;


namespace BDAM
{
    [ProtoContract]
    public class ListComp //Used for mod storage serialization
    {
        [ProtoMember(100)] public List<ListCompItem> compItems = new List<ListCompItem>();
        [ProtoMember(101)] public bool auto = false;
    }

    [ProtoContract]
    public class UpdateComp //Used for mod storage serialization
    {
        [ProtoMember(100)] public List<ListCompItem> compItemsUpdate = new List<ListCompItem>();
        [ProtoMember(101)] public List<string> compItemsRemove = new List<string>();
    }

    [ProtoContract]
    public class ListCompItem
    {
        [ProtoMember(1)] public string bpBase;
        [ProtoMember(2)] public int buildAmount = -1;
        [ProtoMember(3)] public int grindAmount = -1;
        [ProtoMember(4)] public int priority = 3;
        [ProtoMember(5)] public string label;

        public bool missingMats;
        public bool dirty;
    }
}
