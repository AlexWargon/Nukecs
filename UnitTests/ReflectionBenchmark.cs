#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Wargon.Nukecs.Editor;
using Wargon.Nukecs.Editor.EcsDebugV2;

namespace Wargon.Nukecs.Tests
{
    [TestFixture]
    public unsafe class ReflectionBenchmark
    {
        struct SimpleData
        {
            public float x;
            public float y;
            public float z;
            public int id;
            public bool active;
        }

        struct ComplexData
        {
            public float hp;
            public float maxHp;
            public int score;
            public bool alive;
            public float x;
            public float y;
            public float z;
            public int level;
        }

        public class ClassData : IEquatable<ClassData>
        {
            public int x;
            public int y;

            public bool Equals(ClassData other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true;
                return x == other.x && y == other.y;
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ClassData)obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(x, y);
            }
        }

        struct ArrayLikeData
        {
            public int Value;
        }

        const int N = 10000;

        static readonly FieldInfo[] SimpleFields = typeof(SimpleData).GetFields(BindingFlags.Instance | BindingFlags.Public);
        static readonly FieldInfo[] ComplexFields = typeof(ComplexData).GetFields(BindingFlags.Instance | BindingFlags.Public);

        [Test]
        [Performance]
        public void GetField_Simple_RawReflection()
        {
            var obj = (object)new SimpleData { x = 1, y = 2, z = 3, id = 42, active = true };
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        SimpleFields[0].GetValue(obj);
                        SimpleFields[1].GetValue(obj);
                        SimpleFields[2].GetValue(obj);
                        SimpleFields[3].GetValue(obj);
                        SimpleFields[4].GetValue(obj);
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void GetField_Simple_FastReflection()
        {
            var obj = (object)new SimpleData { x = 1, y = 2, z = 3, id = 42, active = true };
            for (var i = 0; i < SimpleFields.Length; i++)
                FastReflectionAccessor.GetValue(typeof(SimpleData), i, obj);

            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        obj.GetFieldValue(0);
                        obj.GetFieldValue(1);
                        obj.GetFieldValue(2);
                        obj.GetFieldValue(3);
                        obj.GetFieldValue(4);
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void SetField_Simple_RawReflection()
        {
            var obj = (object)new SimpleData { x = 1, y = 2, z = 3, id = 42, active = true };
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        SimpleFields[0].SetValue(obj, 10f);
                        SimpleFields[1].SetValue(obj, 20f);
                        SimpleFields[2].SetValue(obj, 30f);
                        SimpleFields[3].SetValue(obj, 100);
                        SimpleFields[4].SetValue(obj, false);
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }
        [Test]
        [Performance]
        public void SetField_Simple_Reflect()
        {
            var obj = new SimpleData { x = 1, y = 2, z = 3, id = 42, active = true };
            var type = Reflect._type.Create(typeof(SimpleData));
            byte* ptr = (byte*)Unsafe.AsPointer(ref obj);
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        type.SetFieldObj(0,ptr, 10f);
                        type.SetFieldObj(1,ptr, 20f);
                        type.SetFieldObj(2,ptr, 30f);
                        type.SetFieldObj(3,ptr, 100);
                        type.SetFieldObj(4,ptr, false);
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            Assert.AreEqual(10f, obj.x);
            Assert.AreEqual(20f, obj.y);
            Assert.AreEqual(30f, obj.z);
            Assert.AreEqual(100, obj.id);
            Assert.AreEqual(false, obj.active);
        }
        [Test]
        [Performance]
        public void SetField_Simple_FastReflection()
        {
            var obj = new SimpleData { x = 1, y = 2, z = 3, id = 42, active = true };

            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        FastReflectionAccessor.SetValue(typeof(SimpleData), 0, obj, 10f);
                        FastReflectionAccessor.SetValue(typeof(SimpleData), 1, obj, 20f);
                        FastReflectionAccessor.SetValue(typeof(SimpleData), 2, obj, 30f);
                        FastReflectionAccessor.SetValue(typeof(SimpleData), 3, obj, 100);
                        FastReflectionAccessor.SetValue(typeof(SimpleData), 4, obj, false);
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void GetField_Complex_RawReflection()
        {
            var obj = (object)new ComplexData
            {
                hp = 100, 
                maxHp = 200, 
                score = 42, 
                x = 1, 
                y = 2, 
                z = 3, 
                level = 5
            };
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                        for (var fi = 0; fi < ComplexFields.Length; fi++)
                            ComplexFields[fi].GetValue(obj);
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void GetField_Complex_FastReflection()
        {
            var obj = (object)new ComplexData { hp = 100, maxHp = 200, score = 42, x = 1, y = 2, z = 3, level = 5 };
            for (var i = 0; i < ComplexFields.Length; i++)
                FastReflectionAccessor.GetValue(typeof(ComplexData), i, obj);

            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                        for (var fi = 0; fi < ComplexFields.Length; fi++)
                            FastReflectionAccessor.GetValue(typeof(ComplexData), fi, obj);
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void GetField_ByIndex_vs_ByName_RawReflection()
        {
            var obj = (object)new SimpleData { x = 1, y = 2, z = 3, id = 42, active = true };
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        typeof(SimpleData).GetField("x").GetValue(obj);
                        typeof(SimpleData).GetField("y").GetValue(obj);
                        typeof(SimpleData).GetField("z").GetValue(obj);
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void GetField_ByName_FastReflection()
        {
            var obj = (object)new SimpleData { x = 1, y = 2, z = 3, id = 42, active = true };
            var getX = FastReflectionAccessor.GetGetter(typeof(SimpleData), "x");
            var getY = FastReflectionAccessor.GetGetter(typeof(SimpleData), "y");
            var getZ = FastReflectionAccessor.GetGetter(typeof(SimpleData), "z");

            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        getX(obj);
                        getY(obj);
                        getZ(obj);
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }
        [Test]
        [Performance]
        public void GetField_ByName_Reflect()
        {
            var obj = (object)new SimpleData { x = 1, y = 2, z = 3, id = 42, active = true };
            byte* ptr = (byte*)Unsafe.AsPointer(ref obj);
            var type = Reflect._type.Create(obj.GetType());
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        type.GetField(0, ptr);
                        type.GetField(1, ptr);
                        type.GetField(2, ptr);
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }
        [Test]
        [Performance]
        public void GetProperty_Length_RawReflection()
        {
            var boxed = (object)new int[16];
            var prop = typeof(int[]).GetProperty("Length");
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                        prop.GetValue(boxed);
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void GetProperty_Length_FastReflection()
        {
            var boxed = (object)new int[16];
            var getter = FastReflectionAccessor.GetPropertyGetter(typeof(int[]), "Length");
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                        getter(boxed);
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void InvokeMethod_RawReflection()
        {
            var list = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            var getItem = typeof(List<int>).GetMethod("get_Item");
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        getItem.Invoke(list, new object[] { 0 });
                        getItem.Invoke(list, new object[] { 3 });
                        getItem.Invoke(list, new object[] { 7 });
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void InvokeMethod_FastReflection()
        {
            var list = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            var del = FastReflectionAccessor.GetMethod(typeof(List<int>), "get_Item", new[] { typeof(int) }, typeof(int));
            var func = (Func<object, object[], object>)del;
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        func(list, new object[] { 0 });
                        func(list, new object[] { 3 });
                        func(list, new object[] { 7 });
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        // =================================================================
        // Pointer-offset reads (ComponentFieldAccessorCache approach)
        // Reads fields via byte* + cached offsets — zero boxing, zero alloc
        // =================================================================

        [Test]
        [Performance]
        public void GetField_Simple_PointerOffset()
        {
            var data = new SimpleData { x = 1, y = 2, z = 3, id = 42, active = true };
            var offX = Marshal.OffsetOf<SimpleData>("x").ToInt32();
            var offY = Marshal.OffsetOf<SimpleData>("y").ToInt32();
            var offZ = Marshal.OffsetOf<SimpleData>("z").ToInt32();
            var offId = Marshal.OffsetOf<SimpleData>("id").ToInt32();
            var offActive = Marshal.OffsetOf<SimpleData>("active").ToInt32();
            byte* ptr = (byte*)Unsafe.AsPointer(ref data);

            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        object _x = *(float*)(ptr + offX);
                        object _y = *(float*)(ptr + offY);
                        object _z = *(float*)(ptr + offZ);
                        object _id = *(int*)(ptr + offId);
                        object _a = *(bool*)(ptr + offActive);
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void SetField_Simple_PointerOffset()
        {
            var data = new SimpleData { x = 1, y = 2, z = 3, id = 42, active = true };
            var offX = Marshal.OffsetOf<SimpleData>("x").ToInt32();
            var offY = Marshal.OffsetOf<SimpleData>("y").ToInt32();
            var offZ = Marshal.OffsetOf<SimpleData>("z").ToInt32();
            var offId = Marshal.OffsetOf<SimpleData>("id").ToInt32();
            var offActive = Marshal.OffsetOf<SimpleData>("active").ToInt32();
            byte* ptr = (byte*)Unsafe.AsPointer(ref data);
            object f0 = 10f;
            object f1 = 20f;
            object f2 = 30f;
            object f3 = 100;
            object f4 = false;
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        *(float*)(ptr + offX) = (float)f0;
                        *(float*)(ptr + offY) = (float)f1;
                        *(float*)(ptr + offZ) = (float)f2;
                        *(int*)(ptr + offId) = (int)f3;
                        *(bool*)(ptr + offActive) = (bool)f4;
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void GetField_Complex_PointerOffset()
        {
            var data = new ComplexData { hp = 100, maxHp = 200, score = 42, x = 1, y = 2, z = 3, level = 5 };
            var offsets = new int[ComplexFields.Length];
            for (var i = 0; i < ComplexFields.Length; i++)
                offsets[i] = Marshal.OffsetOf<ComplexData>(ComplexFields[i].Name).ToInt32();
            byte* ptr = (byte*)Unsafe.AsPointer(ref data);

            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        for (var fi = 0; fi < ComplexFields.Length; fi++)
                        {
                            var ft = ComplexFields[fi].FieldType;
                            if (ft == typeof(float))
                                _ = *(float*)(ptr + offsets[fi]);
                            else if (ft == typeof(int))
                                _ = *(int*)(ptr + offsets[fi]);
                            else if (ft == typeof(bool))
                                _ = *(bool*)(ptr + offsets[fi]);
                        }
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void GetField_Complex_ThreeWay()
        {
            var boxed = (object)new ComplexData { hp = 100, maxHp = 200, score = 42, x = 1, y = 2, z = 3, level = 5 };
            for (var i = 0; i < ComplexFields.Length; i++)
                FastReflectionAccessor.GetValue(typeof(ComplexData), i, boxed);

            var raw = new ComplexData { hp = 100, maxHp = 200, score = 42, x = 1, y = 2, z = 3, level = 5 };
            var offsets = new int[ComplexFields.Length];
            for (var i = 0; i < ComplexFields.Length; i++)
                offsets[i] = Marshal.OffsetOf<ComplexData>(ComplexFields[i].Name).ToInt32();
            byte* ptr = (byte*)Unsafe.AsPointer(ref raw);

            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        for (var fi = 0; fi < ComplexFields.Length; fi++)
                        {
                            var ft = ComplexFields[fi].FieldType;
                            if (ft == typeof(float))
                                _ =  (object) *(float*)(ptr + offsets[fi]);
                            else if (ft == typeof(int))
                                _ = (object)*(int*)(ptr + offsets[fi]);
                            else if (ft == typeof(bool))
                                _ = (object)*(bool*)(ptr + offsets[fi]);
                        }
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }
        [Test]
        [Performance]
        public void GetField_Simple_Reflect()
        {
            var data = new SimpleData { x = 1, y = 2, z = 3, id = 42, active = true };

            byte* ptr = (byte*)Unsafe.AsPointer(ref data);
            var type = Reflect._type.Create(data.GetType());
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        _ = type.GetField(0, ptr);
                        _ = type.GetField(1, ptr);
                        _ = type.GetField(2, ptr);
                        _ = type.GetField(3, ptr);
                        _ = type.GetField(4, ptr);
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();

        }

        public struct ManagedData
        {
            public float zzz;
            public ClassData classData;
        }
        [Test]
        [Performance]
        public void GetField_Managed_Reflect()
        {
            object classData = new ClassData()
            {
                x = 14,
                y = 88
            };
            var data = new ManagedData { zzz = 666, classData = new ClassData()
            {
                x = 14,
                y = 88
            }};

            byte* ptr = (byte*)Unsafe.AsPointer(ref data);
            var type = Reflect._type.Create(data.GetType());
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        _ = type.GetField(0, ptr);
                        _ = type.GetField(1, ptr);
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            Assert.AreEqual(666, type.GetField(0, ptr));
            Assert.AreEqual(classData, type.GetField(1, ptr));
        }
        [Test]
        [Performance]
        public void GetField_Managed_Reflect_object()
        {
            object classData = new ClassData()
            {
                x = 14,
                y = 88
            };
            var data = new ManagedData { zzz = 666, classData = new ClassData()
            {
                x = 14,
                y = 88
            }};

            byte* ptr = (byte*)Unsafe.AsPointer(ref data);
            var type = Reflect._type.Create(data.GetType());
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        _ = type.GetField_object(0, ptr);
                        _ = type.GetField_object(1, ptr);
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            Assert.AreEqual(666, type.GetField_object(0, ptr).float_val);
            Assert.AreEqual(classData, type.GetField_object(1, ptr).object_val);
        }
        [Test]
        [Performance]
        public void GetField_Complex_Reflect_NoBoxing()
        {
            var raw = new ComplexData { hp = 100, maxHp = 200, score = 42, x = 1, y = 2, z = 3, level = 5 };

            byte* ptr = (byte*)Unsafe.AsPointer(ref raw);
            var type = Reflect._type.Create(raw.GetType());
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        for (var fi = 0; fi < ComplexFields.Length; fi++)
                        {
                            var ft = ComplexFields[fi].FieldType;
                            if (ft == typeof(float))
                                _ = (object)type.GetField<float>(fi, ptr);
                            else if (ft == typeof(int))
                                _ = (object)type.GetField<int>(fi, ptr);
                            else if (ft == typeof(bool))
                                _ = (object)type.GetField<bool>(fi, ptr);
                        }
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test]
        [Performance]
        public void GetField_Complex_Reflect()
        {
            var raw = new ComplexData
            {
                hp = 100, 
                maxHp = 200, 
                score = 42, 
                x = 1, 
                y = 2, 
                z = 3, 
                level = 5
            };

            byte* ptr = (byte*)Unsafe.AsPointer(ref raw);
            var type = Reflect._type.Create(raw.GetType());
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        for (var fi = 0; fi < ComplexFields.Length; fi++)
                        {
                            _ = type.GetField(fi, ptr);
                        }
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            
            Assert.AreEqual(100, type.GetField(0, ptr));
            Assert.AreEqual(200, type.GetField(1, ptr));
            Assert.AreEqual(42, type.GetField(2, ptr));
            Assert.AreEqual(false, type.GetField(3, ptr));
            Assert.AreEqual(1, type.GetField(4, ptr));
            Assert.AreEqual(2, type.GetField(5, ptr));
            Assert.AreEqual(3, type.GetField(6, ptr));
            Assert.AreEqual(5, type.GetField(7, ptr));
        }
        
        [Test]
        [Performance]
        public void GetField_Complex_Reflect__object()
        {
            var raw = new ComplexData
            {
                hp = 100, 
                maxHp = 200, 
                score = 42, 
                x = 1, 
                y = 2, 
                z = 3, 
                level = 5
            };

            var ptr = (byte*)Unsafe.AsPointer(ref raw);
            var type = Reflect._type.Create(raw.GetType());
            Measure.Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        for (var fi = 0; fi < type.len; fi++)
                        {
                            _ = type.GetField_object(fi, ptr);
                        }
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();

            Assert.AreEqual(raw.hp, type.GetField_object(0, ptr).float_val);
            Assert.AreEqual(200, type.GetField_object(1, ptr).float_val);
            Assert.AreEqual(42, type.GetField_object(2, ptr).int_val);
            Assert.AreEqual(false, type.GetField_object(3, ptr).bool_val);
            Assert.AreEqual(1, type.GetField_object(4, ptr).float_val);
            Assert.AreEqual(2, type.GetField_object(5, ptr).float_val);
            Assert.AreEqual(3, type.GetField_object(6, ptr).float_val);
            Assert.AreEqual(5, type.GetField_object(7, ptr).int_val);
        }
    }
}
#endif
