// 저장·불러오기: 행성 상태 전체를 파일 하나(gzip)로. 지도(Geo)는 시드로 다시 만들고, [NonSerialized] 필드(잠금·병렬 설정·스레드 임시 공간)는 건너뛴다.
// 필드를 리플렉션으로 훑으므로 상태 필드를 새로 더해도 이 파일을 고칠 필요가 없다. 같은 객체를 두 곳에서 가리키면(계통 칸 ↔ 살아 있는 목록) 번호로 한 번만 쓴다.
// 형식이 바뀐 옛 파일(필드 이름·형이 다름)은 읽다가 멈추고 예외를 낸다: 앱은 그 파일을 옆으로 치우고 새 지구로 시작한다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Life
{
    public static class Save
    {
        const int MAGIC = 0x544F5645, VER = 1;   // "EVOT"
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public sealed class Head { public int seed; public double day, rate; public DateTime at; }   // rate: 저장할 때 속도(일/초), 멈춘 동안 따라잡기에 쓴다

        public static void Write(Planet pl, string path, double rate)
        {
            string tmp = path + ".tmp";
            using (var w = new BinaryWriter(new GZipStream(File.Create(tmp), CompressionLevel.Fastest)))
            {
                w.Write(MAGIC); w.Write(VER); w.Write(pl.seed); w.Write(pl.Day); w.Write(rate); w.Write(DateTime.UtcNow.Ticks);
                new Out(w).Fields(pl);
            }
            if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);   // 다 쓴 뒤에 바꿔 끼운다: 쓰다 꺼져도 옛 파일은 멀쩡
        }

        public static Planet Read(string path, out Head h)
        {
            using var r = new BinaryReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress));
            if (r.ReadInt32() != MAGIC || r.ReadInt32() != VER) throw new InvalidDataException("저장 파일 형식이 다름");
            h = new Head { seed = r.ReadInt32(), day = r.ReadDouble(), rate = r.ReadDouble(), at = new DateTime(r.ReadInt64(), DateTimeKind.Utc) };
            var pl = new Planet(h.seed);
            new In(r).Fields(pl);
            if (pl.B.Length != Cfg.SMAX * pl.V || pl.pool[0].Length != pl.V) throw new InvalidDataException("저장 파일의 칸 수가 지금 설정과 다름");
            return pl;
        }

        static readonly Dictionary<Type, FieldInfo[]> fieldCache = new();
        static FieldInfo[] Fields(Type t)
        {
            lock (fieldCache)
            {
                if (fieldCache.TryGetValue(t, out var fs)) return fs;
                var l = new List<FieldInfo>(); foreach (var f in t.GetFields(F)) if (!f.IsNotSerialized) l.Add(f);
                l.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));   // 이름 순: 빌드마다 필드 순서가 달라도 같은 파일
                return fieldCache[t] = l.ToArray();
            }
        }
        // 형 서명: 객체 형을 처음 쓸 때 함께 적고 읽을 때 맞춰 본다(필드가 바뀐 옛 파일을 잘못 읽지 않게)
        static string Sig(Type t) { var s = new System.Text.StringBuilder(t.FullName); foreach (var f in Fields(t)) s.Append(' ').Append(f.FieldType.Name).Append(':').Append(f.Name); return s.ToString(); }
        static bool Bulk(Type e) => e.IsPrimitive;   // 수 배열은 바이트로 통째로
        static bool IsList(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);

        sealed class Out
        {
            readonly BinaryWriter w; readonly Dictionary<object, int> ids = new(RefEq.I); readonly HashSet<Type> seen = new();
            public Out(BinaryWriter w) => this.w = w;
            public void Fields(object o) { foreach (var f in Save.Fields(o.GetType())) Value(f.FieldType, f.GetValue(o)); }

            void Value(Type t, object v)
            {
                if (t.IsEnum) { w.Write(Convert.ToInt64(v)); return; }
                if (t.IsPrimitive) { Prim(t, v); return; }
                if (t == typeof(string)) { w.Write(v != null); if (v != null) w.Write((string)v); return; }
                if (t.IsValueType) { Struct(t); Fields(v); return; }
                if (v == null) { w.Write((byte)0); return; }
                if (v.GetType() != t) throw new NotSupportedException($"저장: {t} 필드에 {v.GetType()}");
                if (ids.TryGetValue(v, out int id)) { w.Write((byte)1); w.Write(id); return; }
                w.Write((byte)2); ids[v] = ids.Count;
                if (t.IsArray)
                {
                    var a = (Array)v; var e = t.GetElementType(); if (a.Rank != 1) throw new NotSupportedException($"저장: {t}");
                    w.Write(a.Length);
                    if (Bulk(e)) { var b = new byte[Buffer.ByteLength(a)]; Buffer.BlockCopy(a, 0, b, 0, b.Length); w.Write(b); }
                    else for (int i = 0; i < a.Length; i++) Value(e, a.GetValue(i));
                }
                else if (IsList(t)) { var l = (IList)v; var e = t.GetGenericArguments()[0]; w.Write(l.Count); for (int i = 0; i < l.Count; i++) Value(e, l[i]); }
                else if (t.IsGenericType || typeof(Delegate).IsAssignableFrom(t)) throw new NotSupportedException($"저장: {t}([NonSerialized]로 빼거나 지원을 더한다)");
                else { Struct(t); Fields(v); }
            }
            void Struct(Type t) { if (seen.Add(t)) { w.Write(true); w.Write(Sig(t)); } else w.Write(false); }
            void Prim(Type t, object v)
            {
                switch (Type.GetTypeCode(t))
                {
                    case TypeCode.Double: w.Write((double)v); break; case TypeCode.Single: w.Write((float)v); break;
                    case TypeCode.Int32: w.Write((int)v); break; case TypeCode.Int64: w.Write((long)v); break;
                    case TypeCode.UInt64: w.Write((ulong)v); break; case TypeCode.UInt32: w.Write((uint)v); break;
                    case TypeCode.Boolean: w.Write((bool)v); break; case TypeCode.Byte: w.Write((byte)v); break;
                    case TypeCode.Int16: w.Write((short)v); break; case TypeCode.Char: w.Write((char)v); break;
                    default: throw new NotSupportedException($"저장: {t}");
                }
            }
        }

        sealed class In
        {
            readonly BinaryReader r; readonly List<object> objs = new(); readonly HashSet<Type> seen = new();
            public In(BinaryReader r) => this.r = r;

            // 읽은 값을 필드에 넣는다. readonly 참조 필드(목록·배열)는 이미 있는 것을 채워 쓴다
            public void Fields(object o)
            {
                foreach (var f in Save.Fields(o.GetType()))
                {
                    object cur = f.GetValue(o), v = Value(f.FieldType, f.IsInitOnly ? cur : null);
                    if (!ReferenceEquals(v, cur) && !Equals(v, cur)) f.SetValue(o, v);
                }
            }

            object Value(Type t, object reuse)
            {
                if (t.IsEnum) return Enum.ToObject(t, r.ReadInt64());
                if (t.IsPrimitive) return Prim(t);
                if (t == typeof(string)) return r.ReadBoolean() ? r.ReadString() : null;
                if (t.IsValueType) { Struct(t); object s = Activator.CreateInstance(t); Fields(s); return s; }
                byte tag = r.ReadByte();
                if (tag == 0) return null;
                if (tag == 1) return objs[r.ReadInt32()];
                if (t.IsArray)
                {
                    int n = r.ReadInt32(); var e = t.GetElementType();
                    var a = reuse is Array ra && ra.Length == n ? ra : Array.CreateInstance(e, n); objs.Add(a);
                    if (Bulk(e)) { var b = r.ReadBytes(Buffer.ByteLength(a)); Buffer.BlockCopy(b, 0, a, 0, b.Length); }
                    else for (int i = 0; i < n; i++) a.SetValue(Value(e, null), i);
                    return a;
                }
                if (IsList(t))
                {
                    var l = (IList)(reuse ?? Activator.CreateInstance(t)); l.Clear(); objs.Add(l);
                    int n = r.ReadInt32(); var e = t.GetGenericArguments()[0];
                    for (int i = 0; i < n; i++) l.Add(Value(e, null));
                    return l;
                }
                Struct(t);
                var o = reuse ?? New(t); objs.Add(o); Fields(o);
                return o;
            }
            static object New(Type t)
            {
                var c = t.GetConstructor(F, null, Type.EmptyTypes, null);   // 필드 초기값(readonly 배열 등)을 만들려고 생성자를 부른다
#pragma warning disable SYSLIB0050
                return c != null ? c.Invoke(null) : FormatterServices.GetUninitializedObject(t);
#pragma warning restore SYSLIB0050
            }
            void Struct(Type t)
            {
                if (!r.ReadBoolean()) { if (!seen.Contains(t)) throw new InvalidDataException($"저장 파일 순서가 다름({t.Name})"); return; }
                string s = r.ReadString(); seen.Add(t);
                if (s != Sig(t)) throw new InvalidDataException($"저장 파일의 {t.Name} 형식이 지금과 다름");
            }
            object Prim(Type t) => Type.GetTypeCode(t) switch
            {
                TypeCode.Double => r.ReadDouble(), TypeCode.Single => r.ReadSingle(), TypeCode.Int32 => r.ReadInt32(), TypeCode.Int64 => r.ReadInt64(),
                TypeCode.UInt64 => r.ReadUInt64(), TypeCode.UInt32 => r.ReadUInt32(), TypeCode.Boolean => r.ReadBoolean(), TypeCode.Byte => r.ReadByte(),
                TypeCode.Int16 => r.ReadInt16(), TypeCode.Char => r.ReadChar(), _ => throw new NotSupportedException($"불러오기: {t}"),
            };
        }

        sealed class RefEq : IEqualityComparer<object>
        {
            public static readonly RefEq I = new();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }
    }
}
