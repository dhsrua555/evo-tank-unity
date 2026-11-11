// 저장·불러오기: 행성 상태 전체를 파일 하나(gzip)로. 지도(Geo)는 시드로 다시 만들고, [NonSerialized] 필드(잠금·병렬 설정·스레드 임시 공간)는 건너뛴다.
// 필드를 리플렉션으로 훑으므로 상태 필드를 새로 더해도 이 파일을 고칠 필요가 없다. 같은 객체를 두 곳에서 가리키면(계통 칸 ↔ 살아 있는 목록) 번호로 한 번만 쓴다.
// 필드가 바뀐 옛 파일은 이름으로 맞춰 읽는다: 새로 생긴 필드는 처음 값 그대로, 없어진 수 필드는 읽고 버린다(업데이트해도 지구가 이어짐).
// 그 밖(형이 바뀜·없어진 객체 필드)은 읽다가 멈추고 예외를 낸다: 앱은 그 파일을 옆으로 치우고 새 지구로 시작한다.
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
        const int MAGIC = 0x544F5645, VER = 2;   // "EVOT". 2: 맨 위 Planet 형 서명도 적음(1은 없음)
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public sealed class Head { public int seed; public double day, rate; public DateTime at; }   // rate: 저장할 때 정한 속도(일/초, 0 = 최대). 지금은 읽기만 하고 안 씀(옛 '멈춘 동안 따라잡기' 자리, 파일 형식은 그대로)

        public static void Write(Planet pl, string path, double rate)
        {
            string tmp = path + ".tmp";
            using (var w = new BinaryWriter(new GZipStream(File.Create(tmp), CompressionLevel.Fastest)))
            {
                w.Write(MAGIC); w.Write(VER); w.Write(pl.seed); w.Write(pl.Day); w.Write(rate); w.Write(DateTime.UtcNow.Ticks);
                new Out(w).Root(pl);
            }
            if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);   // 다 쓴 뒤에 바꿔 끼운다: 쓰다 꺼져도 옛 파일은 멀쩡
        }

        public static Planet Read(string path, out Head h)
        {
            using var r = new BinaryReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress));
            int ver = r.ReadInt32() == MAGIC ? r.ReadInt32() : -1;
            if (ver != 1 && ver != VER) throw new InvalidDataException("저장 파일 형식이 다름");
            h = new Head { seed = r.ReadInt32(), day = r.ReadDouble(), rate = r.ReadDouble(), at = new DateTime(r.ReadInt64(), DateTimeKind.Utc) };
            var pl = new Planet(h.seed);
            new In(r).Root(pl, ver >= 2);
            if (pl.pool[0].Length == pl.V && pl.B.Length % pl.V == 0 && pl.B.Length / pl.V < Cfg.SMAX) Widen(pl, pl.B.Length / pl.V);
            if (pl.B.Length != Cfg.SMAX * pl.V || pl.pool[0].Length != pl.V) throw new InvalidDataException("저장 파일의 칸 수가 지금 설정과 다름");
            foreach (var sp in pl.species) if (!sp.Alive) sp.g0 = null;   // 08 Speciate: 멸종 종의 창시 유전체는 안 쓴다(그 고침 전 저장)
            foreach (var s in pl.strains) if (s != null) pl.SetGenome(s, s.g);   // 형질·몸·포식 값은 유전체에서 다시 낸다: 공식을 고친 업데이트도 옛 저장의 값을 쓰지 않게(같은 공식이면 저장값과 같음)
            return pl;
        }

        // 계통 칸을 늘린 업데이트(320 → 640)에서 옛 지구를 잇는다: 칸 번호(slot)는 그대로, 새 칸은 빈 채로
        static void Widen(Planet pl, int os)
        {
            int V = pl.V, S = Cfg.SMAX;
            double[] Pad(double[] a) { var b = new double[S * V]; Array.Copy(a, b, a.Length); return b; }   // [slot·V + v]
            var st = new Strain[S]; Array.Copy(pl.strains, st, os); pl.strains = st;
            pl.B = Pad(pl.B); pl.grow = Pad(pl.grow); pl.limA = Pad(pl.limA); pl.eatScale = Pad(pl.eatScale); pl.eatenRate = Pad(pl.eatenRate);
            int nq = pl.inc.Length / (os * V); var inc = new double[nq * S * V];
            for (int q = 0; q < nq; q++) Array.Copy(pl.inc, q * os * V, inc, q * S * V, os * V);   // [(q·SMAX + slot)·V + v]
            pl.inc = inc;
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
            // ids는 지난번 저장의 객체 수로 미리 잡는다(18만 종 지구: 객체 ~130만, 늘릴 때마다 다시 잡으면 수십 MB). buf: 수 배열을 바이트로 옮길 때 다시 쓰는 칸
            static int lastIds = 1024; byte[] buf = new byte[1 << 16];
            readonly BinaryWriter w; readonly Dictionary<object, int> ids = new(lastIds, RefEq.I); readonly HashSet<Type> seen = new();
            public Out(BinaryWriter w) => this.w = w;
            public void Root(object o) { Struct(o.GetType()); Fields(o); lastIds = Math.Max(1024, ids.Count + ids.Count / 8); }
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
                    if (Bulk(e)) { int n = Buffer.ByteLength(a); if (buf.Length < n) buf = new byte[Math.Max(n, 2 * buf.Length)]; Buffer.BlockCopy(a, 0, buf, 0, n); w.Write(buf, 0, n); }
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
            readonly BinaryReader r; readonly List<object> objs = new(); readonly HashSet<Type> seen = new(); byte[] buf = new byte[1 << 16];
            readonly Dictionary<Type, (FieldInfo f, Type skip)[]> old = new();   // 서명이 지금과 다른 형: 옛 파일의 필드 차례
            public In(BinaryReader r) => this.r = r;
            // 판 1 파일(v0.2.3)엔 Planet 서명이 없다: 지금 필드에서 그 뒤에 생긴 것만 빼고 읽는다(새 필드는 처음 값 그대로)
            static readonly HashSet<string> AFTER_V1 = new() { "driftN", "driftShape", "buryP" };
            public void Root(object o, bool sig)
            {
                var t = o.GetType();
                if (sig) Struct(t);
                else { var L = new List<(FieldInfo, Type)>(); foreach (var f in Save.Fields(t)) if (!AFTER_V1.Contains(f.Name)) L.Add((f, null)); old[t] = L.ToArray(); seen.Add(t); }
                Fields(o);
            }

            // 읽은 값을 필드에 넣는다. readonly 참조 필드(목록·배열)는 이미 있는 것을 채워 쓴다
            public void Fields(object o)
            {
                if (!old.TryGetValue(o.GetType(), out var L)) { foreach (var f in Save.Fields(o.GetType())) Put(o, f); return; }
                foreach (var (f, skip) in L) if (f != null) Put(o, f); else Prim(skip);
            }
            void Put(object o, FieldInfo f)
            {
                object cur = f.GetValue(o), v = Value(f.FieldType, f.IsInitOnly ? cur : null);
                if (!ReferenceEquals(v, cur) && !Equals(v, cur)) f.SetValue(o, v);
            }
            // 옛 서명("형이름 수형:필드 …")을 지금 필드에 이름으로 잇는다. 지금 없는 수 필드는 버릴 형만 적는다
            static string Norm(string s) => System.Text.RegularExpressions.Regex.Replace(s, @", [^\[\]]+\]", "]");   // "[System.Int32, mscorlib, Version=…]" → "[System.Int32]"
            static (FieldInfo, Type)[] Map(Type t, string s)
            {
                var p = s.Split(' '); var now = new Dictionary<string, FieldInfo>(); foreach (var f in Save.Fields(t)) now[f.Name] = f;
                if (p[0] != t.FullName) throw new InvalidDataException($"저장 파일 순서가 다름({t.Name})");
                var L = new (FieldInfo, Type)[p.Length - 1];
                for (int i = 1; i < p.Length; i++)
                {
                    int k = p[i].IndexOf(':'); string tn = p[i][..k], nm = p[i][(k + 1)..];
                    var pt = Type.GetType("System." + tn);
                    if (now.TryGetValue(nm, out var f) && f.FieldType.Name == tn) L[i - 1] = (f, null);
                    else if (!now.ContainsKey(nm) && pt != null && pt.IsPrimitive) L[i - 1] = (null, pt);
                    else throw new InvalidDataException($"저장 파일의 {t.Name}.{nm} 형식이 지금과 다름");
                }
                return L;
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
                    if (Bulk(e))
                    {
                        int nb = Buffer.ByteLength(a); if (buf.Length < nb) buf = new byte[Math.Max(nb, 2 * buf.Length)];
                        for (int off = 0; off < nb;) { int k = r.Read(buf, off, nb - off); if (k <= 0) throw new EndOfStreamException(); off += k; }
                        Buffer.BlockCopy(buf, 0, a, 0, nb);
                    }
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
                if (s != Sig(t) && Norm(s) != Norm(Sig(t))) old[t] = Map(t, s);   // Norm: 제네릭 형 이름의 어셈블리 꼬리표는 런타임마다 다르다(.NET LifeTest 저장 ↔ 앱 Mono)
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
