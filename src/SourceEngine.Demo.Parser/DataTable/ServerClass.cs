using System;
using System.Collections.Generic;

using SourceEngine.Demo.Parser.Packet;

namespace SourceEngine.Demo.Parser.DataTable
{
    internal class ServerClass : IDisposable
    {
        public List<ServerClass> BaseClasses = new();
        public int ClassID;
        public int DataTableID;
        public string DTName;

        public readonly List<FlattenedPropEntry> FlattenedProps = new();
        public string Name;

        public void Dispose()
        {
            OnNewEntity = null;
            OnDestroyEntity = null;
        }

        public event EventHandler<EntityCreatedEventArgs> OnNewEntity;

        internal void AnnounceNewEntity(Entity e)
        {
            if (OnNewEntity != null)
                OnNewEntity(this, new EntityCreatedEventArgs(this, e));
        }

        public event EventHandler<EntityDestroyedEventArgs> OnDestroyEntity;

        internal void AnnounceDestroyedEntity(Entity e)
        {
            if (OnDestroyEntity != null)
                OnDestroyEntity(this, new EntityDestroyedEventArgs(this, e));
        }

        public override string ToString()
        {
            return Name + " | " + DTName;
        }
    }

    internal class FlattenedPropEntry
    {
        public FlattenedPropEntry(string propertyName, SendTableProperty prop, SendTableProperty arrayElementProp)
        {
            Prop = prop;
            ArrayElementProp = arrayElementProp;
            PropertyName = propertyName;
        }

        public SendTableProperty Prop { get; private set; }

        public SendTableProperty ArrayElementProp { get; private set; }

        public string PropertyName { get; private set; }

        public override string ToString()
        {
            return string.Format(
                "[FlattenedPropEntry: PropertyName={2}, Prop={0}, ArrayElementProp={1}]",
                Prop,
                ArrayElementProp,
                PropertyName
            );
        }
    }

    internal class ExcludeEntry
    {
        public ExcludeEntry(string varName, string dtName, string excludingDT)
        {
            VarName = varName;
            DTName = dtName;
            ExcludingDT = excludingDT;
        }

        public string VarName { get; private set; }

        public string DTName { get; private set; }

        public string ExcludingDT { get; private set; }
    }

    internal class EntityCreatedEventArgs : EventArgs
    {
        public EntityCreatedEventArgs(ServerClass c, Entity e)
        {
            Class = c;
            Entity = e;
        }

        public ServerClass Class { get; private set; }

        public Entity Entity { get; private set; }
    }

    internal class EntityDestroyedEventArgs : EntityCreatedEventArgs
    {
        public EntityDestroyedEventArgs(ServerClass c, Entity e) : base(c, e) { }
    }
}
