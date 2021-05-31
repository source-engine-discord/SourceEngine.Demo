﻿using System;
using System.Collections.Generic;
using System.Linq;

using SourceEngine.Demo.Parser.BitStream;
using SourceEngine.Demo.Parser.Messages;

namespace SourceEngine.Demo.Parser.DataTable
{
    internal class DataTableParser
    {
        private readonly List<ExcludeEntry> CurrentExcludes = new();

        public readonly List<SendTable> DataTables = new();
        public readonly List<ServerClass> ServerClasses = new();
        private List<ServerClass> CurrentBaseclasses = new();

        public int ClassBits => (int)Math.Ceiling(Math.Log(ServerClasses.Count, 2));

        public void ParsePacket(IBitStream bitstream)
        {
            while (true)
            {
                var type = (SVC_Messages)bitstream.ReadProtoInt32();
                if (type != SVC_Messages.svc_SendTable)
                    throw new Exception("Expected SendTable, got " + type);

                var size = bitstream.ReadProtoInt32();
                bitstream.BeginChunk(size * 8);
                var sendTable = new SendTable(bitstream);
                bitstream.EndChunk();

                if (sendTable.IsEnd)
                    break;

                DataTables.Add(sendTable);
            }

            int serverClassCount = checked((int)bitstream.ReadInt(16));

            for (int i = 0; i < serverClassCount; i++)
            {
                var entry = new ServerClass();
                entry.ClassID = checked((int)bitstream.ReadInt(16));

                if (entry.ClassID > serverClassCount)
                    throw new Exception("Invalid class index");

                entry.Name = bitstream.ReadDataTableString();
                entry.DTName = bitstream.ReadDataTableString();

                entry.DataTableID = DataTables.FindIndex(a => a.Name == entry.DTName);

                ServerClasses.Add(entry);
            }

            for (int i = 0; i < serverClassCount; i++)
                FlattenDataTable(i);
        }

        private void FlattenDataTable(int serverClassIndex)
        {
            SendTable table = DataTables[ServerClasses[serverClassIndex].DataTableID];

            CurrentExcludes.Clear();
            CurrentBaseclasses = new List<ServerClass>(); //NOT .clear because we use *this* reference

            //LITERALLY 3 lines later. @main--, this is warning for you.

            GatherExcludesAndBaseclasses(table, true);

            ServerClasses[serverClassIndex].BaseClasses = CurrentBaseclasses;

            GatherProps(table, serverClassIndex, "");

            List<FlattenedPropEntry> flattenedProps = ServerClasses[serverClassIndex].FlattenedProps;

            var priorities = new List<int> { 64 };
            priorities.AddRange(flattenedProps.Select(a => a.Prop.Priority).Distinct());
            priorities.Sort();

            int start = 0;

            foreach (int priority in priorities)
            {
                while (true)
                {
                    int currentProp = start;

                    while (currentProp < flattenedProps.Count)
                    {
                        SendTableProperty prop = flattenedProps[currentProp].Prop;

                        if (prop.Priority == priority
                            || priority == 64 && prop.Flags.HasFlagFast(SendPropertyFlags.ChangesOften))
                        {
                            if (start != currentProp)
                            {
                                FlattenedPropEntry temp = flattenedProps[start];
                                flattenedProps[start] = flattenedProps[currentProp];
                                flattenedProps[currentProp] = temp;
                            }

                            start++;
                            break;
                        }

                        currentProp++;
                    }

                    if (currentProp == flattenedProps.Count)
                        break;
                }
            }
        }

        private void GatherExcludesAndBaseclasses(SendTable sendTable, bool collectBaseClasses)
        {
            CurrentExcludes.AddRange(
                sendTable.Properties.Where(a => a.Flags.HasFlagFast(SendPropertyFlags.Exclude))
                    .Select(a => new ExcludeEntry(a.Name, a.DataTableName, sendTable.Name))
            );

            foreach (SendTableProperty prop in sendTable.Properties.Where(a => a.Type == SendPropertyType.DataTable))
            {
                if (collectBaseClasses && prop.Name == "baseclass")
                {
                    GatherExcludesAndBaseclasses(GetTableByName(prop.DataTableName), true);
                    CurrentBaseclasses.Add(FindByDTName(prop.DataTableName));
                }
                else
                {
                    GatherExcludesAndBaseclasses(GetTableByName(prop.DataTableName), false);
                }
            }
        }

        private void GatherProps(SendTable table, int serverClassIndex, string prefix)
        {
            var tmpFlattenedProps = new List<FlattenedPropEntry>();
            GatherProps_IterateProps(table, serverClassIndex, tmpFlattenedProps, prefix);

            List<FlattenedPropEntry> flattenedProps = ServerClasses[serverClassIndex].FlattenedProps;

            flattenedProps.AddRange(tmpFlattenedProps);
        }

        private void GatherProps_IterateProps(
            SendTable table,
            int ServerClassIndex,
            ICollection<FlattenedPropEntry> flattenedProps,
            string prefix)
        {
            for (int i = 0; i < table.Properties.Count; i++)
            {
                SendTableProperty property = table.Properties[i];

                if (property.Flags.HasFlagFast(SendPropertyFlags.InsideArray)
                    || property.Flags.HasFlagFast(SendPropertyFlags.Exclude) || IsPropExcluded(table, property))
                    continue;

                if (property.Type == SendPropertyType.DataTable)
                {
                    SendTable subTable = GetTableByName(property.DataTableName);

                    if (property.Flags.HasFlagFast(SendPropertyFlags.Collapsible))
                    {
                        //we don't prefix Collapsible stuff, since it is just derived mostly
                        GatherProps_IterateProps(subTable, ServerClassIndex, flattenedProps, prefix);
                    }
                    else
                    {
                        //We do however prefix everything else

                        string nfix = prefix + (property.Name.Length > 0 ? property.Name + "." : "");

                        GatherProps(subTable, ServerClassIndex, nfix);
                    }
                }
                else
                {
                    if (property.Type == SendPropertyType.Array)
                        flattenedProps.Add(
                            new FlattenedPropEntry(prefix + property.Name, property, table.Properties[i - 1])
                        );
                    else
                        flattenedProps.Add(new FlattenedPropEntry(prefix + property.Name, property, null));
                }
            }
        }

        private bool IsPropExcluded(SendTable table, SendTableProperty prop)
        {
            return CurrentExcludes.Exists(a => table.Name == a.DTName && prop.Name == a.VarName);
        }

        private SendTable GetTableByName(string pName)
        {
            return DataTables.FirstOrDefault(a => a.Name == pName);
        }

        public ServerClass FindByName(string className)
        {
            return ServerClasses.Single(a => a.Name == className);
        }

        private ServerClass FindByDTName(string dtName)
        {
            return ServerClasses.Single(a => a.DTName == dtName);
        }
    }
}
