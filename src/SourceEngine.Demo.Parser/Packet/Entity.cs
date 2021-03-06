using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using SourceEngine.Demo.Parser.DataTable;
using SourceEngine.Demo.Parser.Packet.Handler;

namespace SourceEngine.Demo.Parser.Packet
{
    internal class Entity
    {
        public Entity(int id, ServerClass serverClass)
        {
            ID = id;
            ServerClass = serverClass;

            var flattenedProps = ServerClass.FlattenedProps;
            Props = new PropertyEntry[flattenedProps.Count];
            for (int i = 0; i < flattenedProps.Count; i++)
                Props[i] = new PropertyEntry(flattenedProps[i], i);
        }

        public int ID { get; set; }

        public ServerClass ServerClass { get; set; }

        public PropertyEntry[] Props { get; private set; }

        public PropertyEntry FindProperty(string name)
        {
            return Props.Single(a => a.Entry.PropertyName == name);
        }

        /// <summary>
        /// Applies the update.
        /// </summary>
        /// <param name="reader">Reader.</param>
        public void ApplyUpdate(IBitStream reader)
        {
            //Okay, how does an entity-update look like?
            //First a list of the updated props is sent
            //And then the props itself are sent.

            //Read the field-indices in a "new" way?
            bool newWay = reader.ReadBit();
            int index = -1;
            var entries = new List<PropertyEntry>();

            //No read them.
            while ((index = ReadFieldIndex(reader, index, newWay)) != -1)
                entries.Add(Props[index]);

            //Now read the updated props
            foreach (var prop in entries)
                prop.Decode(reader, this);
        }

        private static int ReadFieldIndex(IBitStream reader, int lastIndex, bool bNewWay)
        {
            if (bNewWay)
                if (reader.ReadBit())
                    return lastIndex + 1;

            int ret = 0;

            if (bNewWay && reader.ReadBit())
            {
                ret = (int)reader.ReadInt(3); // read 3 bits
            }
            else
            {
                ret = (int)reader.ReadInt(7); // read 7 bits

                switch (ret & (32 | 64))
                {
                    case 32:
                        ret = (ret & ~96) | ((int)reader.ReadInt(2) << 5);
                        break;
                    case 64:
                        ret = (ret & ~96) | ((int)reader.ReadInt(4) << 5);
                        break;
                    case 96:
                        ret = (ret & ~96) | ((int)reader.ReadInt(7) << 5);
                        break;
                }
            }

            if (ret == 0xFFF)

                // end marker is 4095 for cs:go
                return -1;

            return lastIndex + 1 + ret;
        }

        public void Leave()
        {
            foreach (var prop in Props)
                prop.Destroy();
        }

        public override string ToString()
        {
            return ID + ": " + ServerClass;
        }
    }

    internal class PropertyEntry
    {
        public readonly int Index;

        public FlattenedPropEntry Entry { get; private set; }

        public event EventHandler<PropertyUpdateEventArgs<int>> IntRecived;

        public event EventHandler<PropertyUpdateEventArgs<long>> Int64Received;

        public event EventHandler<PropertyUpdateEventArgs<float>> FloatRecived;

        public event EventHandler<PropertyUpdateEventArgs<Vector>> VectorRecived;

        public event EventHandler<PropertyUpdateEventArgs<string>> StringRecived;

        public event EventHandler<PropertyUpdateEventArgs<object[]>> ArrayRecived;

        #if SAVE_PROP_VALUES
        public object Value { get; private set; }
        #endif

        /*
         * DON'T USE THIS.
         * SERIOUSLY, NO!
         * THERE IS ONLY _ONE_ PATTERN WHERE THIS IS OKAY.
         *
         * SendTableParser.FindByName("CBaseTrigger").OnNewEntity += (s1, newResource) => {
         *
                Dictionary<string, object> values = new Dictionary<string, object>();
                foreach(var res in newResource.Entity.Props)
                {
                    res.DataRecived += (sender, e) => values[e.Property.Entry.PropertyName] = e.Value;
                }

         *
         * The single purpose for this is to see what kind of values an entity has. You can check this faster with this thing.
         * Really, ignore it if you don't know what you're doing.
         */
        [Obsolete(
            "Don't use this attribute. It is only avaible for debugging. Bind to the correct event instead.",
            false
        )]
        #pragma warning disable 0067 // this is unused in release builds, just as it should be
        public event EventHandler<PropertyUpdateEventArgs<object>> DataRecivedDontUse;
        #pragma warning restore 0067

        //[Conditional("DEBUG")]
        private void FireDataReceived_DebugEvent(object val, Entity e)
        {
            #pragma warning disable 0618
            if (DataRecivedDontUse != null)
                DataRecivedDontUse(this, new PropertyUpdateEventArgs<object>(val, e, this));
            #pragma warning restore 0618
        }

        //[Conditional("DEBUG")]
        private void DeleteDataRecived()
        {
            #pragma warning disable 0618
            DataRecivedDontUse = null;
            #pragma warning restore 0618
        }

        public void Decode(IBitStream stream, Entity e)
        {
            //I found no better place for this, sorry.
            //This checks, when in Debug-Mode
            //whether you've bound to the right event
            //Helps finding bugs, where you'd simply miss an update
            CheckBindings(e);

            //So here you start decoding. If you really want
            //to implement this yourself, GOOD LUCK.
            //also, be warned: They have 11 ways to read floats.
            //oh, btw: You may want to read the original Valve-code for this.
            switch (Entry.Prop.Type)
            {
                case SendPropertyType.Int:
                {
                    var val = PropDecoder.DecodeInt(Entry.Prop, stream);
                    if (IntRecived != null)
                        IntRecived(this, new PropertyUpdateEventArgs<int>(val, e, this));

                    SaveValue(val);
                    FireDataReceived_DebugEvent(val, e);
                }

                    break;
                case SendPropertyType.Int64:
                {
                    var val = PropDecoder.DecodeInt64(Entry.Prop, stream);
                    if (Int64Received != null)
                        Int64Received(this, new PropertyUpdateEventArgs<long>(val, e, this));

                    SaveValue(val);
                    FireDataReceived_DebugEvent(val, e);
                }

                    break;
                case SendPropertyType.Float:
                {
                    var val = PropDecoder.DecodeFloat(Entry.Prop, stream);
                    if (FloatRecived != null)
                        FloatRecived(this, new PropertyUpdateEventArgs<float>(val, e, this));

                    SaveValue(val);
                    FireDataReceived_DebugEvent(val, e);
                }

                    break;
                case SendPropertyType.Vector:
                {
                    var val = PropDecoder.DecodeVector(Entry.Prop, stream);
                    if (VectorRecived != null)
                        VectorRecived(this, new PropertyUpdateEventArgs<Vector>(val, e, this));

                    SaveValue(val);
                    FireDataReceived_DebugEvent(val, e);
                }

                    break;
                case SendPropertyType.Array:
                {
                    var val = PropDecoder.DecodeArray(Entry, stream);
                    if (ArrayRecived != null)
                        ArrayRecived(this, new PropertyUpdateEventArgs<object[]>(val, e, this));

                    SaveValue(val);
                    FireDataReceived_DebugEvent(val, e);
                }

                    break;
                case SendPropertyType.String:
                {
                    var val = PropDecoder.DecodeString(Entry.Prop, stream);
                    if (StringRecived != null)
                        StringRecived(this, new PropertyUpdateEventArgs<string>(val, e, this));

                    SaveValue(val);
                    FireDataReceived_DebugEvent(val, e);
                }

                    break;
                case SendPropertyType.VectorXY:
                {
                    var val = PropDecoder.DecodeVectorXY(Entry.Prop, stream);
                    if (VectorRecived != null)
                        VectorRecived(this, new PropertyUpdateEventArgs<Vector>(val, e, this));

                    SaveValue(val);
                    FireDataReceived_DebugEvent(val, e);
                }

                    break;
                default:
                    throw new NotImplementedException("Could not read property. Abort! ABORT! (is it a long?)");
            }
        }

        public PropertyEntry(FlattenedPropEntry prop, int index)
        {
            Entry = new FlattenedPropEntry(prop.PropertyName, prop.Prop, prop.ArrayElementProp);
            Index = index;
        }

        public void Destroy()
        {
            IntRecived = null;
            Int64Received = null;
            FloatRecived = null;
            ArrayRecived = null;
            StringRecived = null;
            VectorRecived = null;

            DeleteDataRecived();
        }

        [Conditional("SAVE_PROP_VALUES")]
        private static void SaveValue(object value)
        {
            #if SAVE_PROP_VALUES
            this.Value = value;
            #endif
        }

        public override string ToString()
        {
            return string.Format("[PropertyEntry: Entry={0}]", Entry);
        }

        //[Conditional("DEBUG")]
        public void CheckBindings(Entity e)
        {
            if (IntRecived != null && Entry.Prop.Type != SendPropertyType.Int)
                throw new InvalidOperationException(
                    string.Format(
                        "({0}).({1}) isn't an {2}",
                        e.ServerClass.Name,
                        Entry.PropertyName,
                        SendPropertyType.Int
                    )
                );

            if (Int64Received != null && Entry.Prop.Type != SendPropertyType.Int64)
                throw new InvalidOperationException(
                    string.Format(
                        "({0}).({1}) isn't an {2}",
                        e.ServerClass.Name,
                        Entry.PropertyName,
                        SendPropertyType.Int64
                    )
                );

            if (FloatRecived != null && Entry.Prop.Type != SendPropertyType.Float)
                throw new InvalidOperationException(
                    string.Format(
                        "({0}).({1}) isn't an {2}",
                        e.ServerClass.Name,
                        Entry.PropertyName,
                        SendPropertyType.Float
                    )
                );

            if (StringRecived != null && Entry.Prop.Type != SendPropertyType.String)
                throw new InvalidOperationException(
                    string.Format(
                        "({0}).({1}) isn't an {2}",
                        e.ServerClass.Name,
                        Entry.PropertyName,
                        SendPropertyType.String
                    )
                );

            if (ArrayRecived != null && Entry.Prop.Type != SendPropertyType.Array)
                throw new InvalidOperationException(
                    string.Format(
                        "({0}).({1}) isn't an {2}",
                        e.ServerClass.Name,
                        Entry.PropertyName,
                        SendPropertyType.Array
                    )
                );

            if (VectorRecived != null && Entry.Prop.Type != SendPropertyType.Vector
                && Entry.Prop.Type != SendPropertyType.VectorXY)
                throw new InvalidOperationException(
                    string.Format(
                        "({0}).({1}) isn't an {2}",
                        e.ServerClass.Name,
                        Entry.PropertyName,
                        SendPropertyType.Vector
                    )
                );
        }

        public static void Emit(Entity entity, IEnumerable<object> captured)
        {
            foreach (var arg in captured)
            {
                if (arg is RecordedPropertyUpdate<int> intReceived)
                {
                    var e = entity.Props[intReceived.PropIndex].IntRecived;

                    if (e != null)
                        e(
                            null,
                            new PropertyUpdateEventArgs<int>(
                                intReceived.Value,
                                entity,
                                entity.Props[intReceived.PropIndex]
                            )
                        );
                }
                else if (arg is RecordedPropertyUpdate<long> int64Received)
                {
                    var e = entity.Props[int64Received.PropIndex].Int64Received;

                    if (e != null)
                        e(
                            null,
                            new PropertyUpdateEventArgs<long>(
                                int64Received.Value,
                                entity,
                                entity.Props[int64Received.PropIndex]
                            )
                        );
                }
                else if (arg is RecordedPropertyUpdate<float> floatReceived)
                {
                    var e = entity.Props[floatReceived.PropIndex].FloatRecived;

                    if (e != null)
                        e(
                            null,
                            new PropertyUpdateEventArgs<float>(
                                floatReceived.Value,
                                entity,
                                entity.Props[floatReceived.PropIndex]
                            )
                        );
                }
                else if (arg is RecordedPropertyUpdate<Vector> vectorReceived)
                {
                    var e = entity.Props[vectorReceived.PropIndex].VectorRecived;

                    if (e != null)
                        e(
                            null,
                            new PropertyUpdateEventArgs<Vector>(
                                vectorReceived.Value,
                                entity,
                                entity.Props[vectorReceived.PropIndex]
                            )
                        );
                }
                else if (arg is RecordedPropertyUpdate<string> stringReceived)
                {
                    var e = entity.Props[stringReceived.PropIndex].StringRecived;

                    if (e != null)
                        e(
                            null,
                            new PropertyUpdateEventArgs<string>(
                                stringReceived.Value,
                                entity,
                                entity.Props[stringReceived.PropIndex]
                            )
                        );
                }
                else if (arg is RecordedPropertyUpdate<object[]> arrayReceived)
                {
                    var e = entity.Props[arrayReceived.PropIndex].ArrayRecived;

                    if (e != null)
                        e(
                            null,
                            new PropertyUpdateEventArgs<object[]>(
                                arrayReceived.Value,
                                entity,
                                entity.Props[arrayReceived.PropIndex]
                            )
                        );
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }
    }

    #region Update-Types

    internal class PropertyUpdateEventArgs<T> : EventArgs
    {
        public PropertyUpdateEventArgs(T value, Entity e, PropertyEntry p)
        {
            Value = value;
            Entity = e;
            Property = p;
        }

        public T Value { get; private set; }

        public Entity Entity { get; private set; }

        public PropertyEntry Property { get; private set; }
    }

    public class RecordedPropertyUpdate<T>
    {
        public readonly int PropIndex;
        public readonly T Value;

        public RecordedPropertyUpdate(int propIndex, T value)
        {
            PropIndex = propIndex;
            Value = value;
        }
    }

    #endregion
}
