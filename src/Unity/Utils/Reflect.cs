
// ReSharper disable InconsistentNaming

namespace Wargon.Nukecs.Reflect
{
    public struct _object
    {
        public bool bool_val;
        public long long_val;
        public double double_val;
        public Entity entity_val;
        public object object_val;
        public _field_type field_type;
        public byte byte_val
        {
            get => (byte)long_val;
            set => long_val = value;
        }
        public short short_val
        {
            get => (short)long_val;
            set => long_val = value;
        }
        public int int_val
        {
            get => (int)long_val;
            set => long_val = value;
        }
        public float float_val
        {
            get => (float)double_val;
            set => double_val = value;
        }

        public static implicit operator bool(_object val) => val.bool_val;
        public static implicit operator byte(_object val) => val.byte_val;
        public static implicit operator short(_object val) => val.short_val;
        public static implicit operator int(_object val) => val.int_val;
        public static implicit operator long(_object val) => val.long_val;
        public static implicit operator float(_object val) => val.float_val;
        public static implicit operator double(_object val) => val.double_val;
        public static implicit operator Entity(_object val) => val.entity_val;
        public static implicit operator _object(bool val) => new (){bool_val = val};
        public static implicit operator _object( byte val) => new (){byte_val = val};
        public static implicit operator _object(short val) => new (){short_val = val};
        public static implicit operator _object(int val) => new (){int_val = val};
        public static implicit operator _object(long val) => new (){long_val = val};
        public static implicit operator _object(float val) => new (){float_val = val};
        public static implicit operator _object(double val) => new (){double_val = val};
        public static implicit operator _object(Entity val) => new (){entity_val = val};
        public static object ToObject(_object value) => value.object_val;
    }
}
