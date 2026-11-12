// 3단계 — 앱 뼈대. 화면 전체가 물속(4–6단계: 한 해역 물속을 헤엄치며 세포를 본다)이고, 그 위에 반투명 판이 뜬다:
// 해역 지도·수심계·축척(7단계), 윗줄·종 목록·그래프·계통수(8단계). 시뮬레이션은 1단계 스레드에서 따로 돈다.
using System;
using System.Collections.Generic;
using System.IO;
using Life;
using UnityEngine;

namespace Tank
{
    public sealed partial class Tank : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { var go = new GameObject("EvoTank"); go.AddComponent<Camera>(); go.AddComponent<Tank>(); }

        Runner run; Snap snap, lastSnap;
        readonly List<DayStat> hist = new();
        readonly List<Life.Event> news = new();
        Camera cam;
        int sel = -1;                            // 고른 종
        static bool newEarth;                    // '새 지구로 시작'으로 다시 만든 Tank는 저장 파일을 읽지 않는다
        string note; float noteUntil;            // 잠깐 뜨는 알림(이어서 돌림 · 못 읽음)

        // 화면 배치(GUI 단위). 3D는 화면 전체, 판은 그 위에 뜬다
        float W, H, topH = 46, panelW = 340, graphH = 120;
        Rect listR, graphR, infoR;
        Vector2 mouse; bool inView;              // 마우스(GUI 단위, 위가 0), 판이 아닌 3D 화면 위인가

        void Awake()
        {
            Application.runInBackground = true;          // 창을 내려도 화면이 계속(시뮬레이션은 어차피 따로 돈다)
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Application.isMobilePlatform ? 30 : 60;   // 컴퓨터 60 fps(화면 스레드 일은 프레임마다 ~3 ms), 휴대폰은 전지·발열 때문에 30
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            cam = GetComponent<Camera>(); cam.fieldOfView = 50;
            // 시드나 사진(-shots)을 준 시험 실행은 저장 파일을 읽지도 쓰지도 않는다(사용자 지구를 덮지 않게)
            bool shots = Array.IndexOf(Environment.GetCommandLineArgs(), "-shots") >= 0, fresh = newEarth; newEarth = false;
            int seedArg = Arg("-seed", -1); bool test = seedArg >= 0 || shots;
            string path = test ? null : Path.Combine(Application.persistentDataPath, "earth.sav");
            bool crashed = CrashLogInit(!test && Array.IndexOf(Environment.GetCommandLineArgs(), "-load") < 0);
            Planet pl = null; Life.Save.Head h = null; var rd = System.Diagnostics.Stopwatch.StartNew();
            int li = Array.IndexOf(Environment.GetCommandLineArgs(), "-load");   // -load <파일>: 그 지구를 읽어 돌리되 저장은 안 함(렉 재기·시험)
            if (li >= 0) { path = null; pl = Life.Save.Read(Environment.GetCommandLineArgs()[li + 1], out h); }
            if (path != null && !fresh && File.Exists(path))
                try { pl = Life.Save.Read(path, out h); Note($"저장한 지구를 이어서 돌립니다 · {h.day:0}일"); }
                catch (Exception e)
                {
                    Debug.LogWarning("[EvoTank] 저장 파일을 못 읽음: " + e.Message);
                    try { File.Copy(path, path + ".old", true); } catch { }
                    Note("저장 파일을 못 읽어 새 지구로 시작합니다(옛 파일은 earth.sav.old)");
                }
            pl ??= new Planet(seedArg >= 0 && !fresh ? seedArg : Environment.TickCount & 0xffff);
            long readMs = rd.ElapsedMilliseconds; run = new Runner(pl, path);
            if (crashed) Note((note != null ? note + " · " : "") + "지난번에 앱이 갑자기 꺼졌습니다(사건 ▼ 메뉴 맨 아래에 기록)");
            Fonts(); LookInit(); LoadPrefs();
            if (shots) StartCoroutine(Shots());
            Debug.Log($"[EvoTank] 시작: 시드 {pl.seed}, {pl.Day:0}일, 해역 {pl.NR}, 칸 {pl.V}, 종 {pl.species.Count}(살아 있음 {pl.nAliveSp}), 저장 {path ?? "안 함"} · 읽기 {readMs} ms · 첫 사진까지 {rd.ElapsedMilliseconds - readMs} ms");
        }

        void OnDestroy() => run?.Stop();
        void OnApplicationQuit() { run?.Stop(); run?.SaveNow(true); Running(false); }
        void Note(string s) { note = s; noteUntil = Time.unscaledTime + 6; }

        // 새 지구: 지금 시뮬레이션을 멈추고 저장 파일을 지운 뒤, 화면(Tank)째로 새로 만든다(물속 위치·고른 종·그래프도 처음부터)
        void NewEarth()
        {
            run.Stop();
            try { if (run.savePath != null) File.Delete(run.savePath); } catch { }
            newEarth = true; enabled = false;
            Destroy(gameObject); Boot();
        }

        static int Arg(string name, int def)
        {
            var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length && int.TryParse(a[i + 1], out int x) ? x : def;
        }

        // 휴대폰·태블릿: 내리면 바로 저장(그대로 꺼져도 이어진다). 내려가 있던 동안의 날은 따라잡지 않고, 다시 열면 멈춘 곳에서 이어 간다
        // 내려가 있는 동안은 시뮬레이션도 쉰다(뒤에서 CPU를 다 쓰면 안드로이드가 앱을 죽인다)
        bool bgPaused;
        void OnApplicationPause(bool p)
        {
            if (run == null) return;
            if (p) { bgPaused = !run.paused; run.paused = true; run.SaveNow(true); Running(false); }
            else { if (bgPaused) run.paused = false; bgPaused = false; Running(true); }
        }

        // 튕김 기록: 휴대폰은 로그를 볼 길이 없으니 오류·예외·메모리 부족을 errors.txt에 남기고 사건 메뉴 아래에 보여 준다.
        // 앱이 앞에 있는 동안 'running' 표시를 켜 두고 내려가거나 끌 때 끈다 → 다음 실행에서 켜져 있으면 갑자기 꺼진 것
        static string errPath, errTail = ""; static int errN; static bool track; static readonly object errLock = new();
        static void ErrLog(string s)
        {
            lock (errLock)
                try
                {
                    if (new FileInfo(errPath) is { Exists: true, Length: > 200_000 }) File.Delete(errPath);
                    File.AppendAllText(errPath, $"{DateTime.Now:MM-dd HH:mm:ss} {s}\n");
                    errN++; errTail = s.Split('\n')[0]; if (errTail.Length > 160) errTail = errTail[..160];
                }
                catch { }
        }
        static void Running(bool on) { if (!track) return; PlayerPrefs.SetInt("running", on ? 1 : 0); PlayerPrefs.Save(); }
        static bool CrashLogInit(bool user)
        {
            if (errPath != null) return false;   // 새 지구로 Tank를 다시 만들어도 한 번만
            errPath = Path.Combine(Application.persistentDataPath, "errors.txt");
            Application.logMessageReceivedThreaded += (m, st, t) => { if (t is LogType.Error or LogType.Exception or LogType.Assert) ErrLog($"{t}: {m}\n{st}"); };
            Application.lowMemory += () => ErrLog("메모리 부족 경고(안드로이드)");
            try { if (File.Exists(errPath)) errTail = System.Linq.Enumerable.LastOrDefault(File.ReadLines(errPath), l => l.Length > 15 && char.IsDigit(l[0]) && l[2] == '-') ?? ""; } catch { }   // 지난 실행의 마지막 기록
            if (!user) return false;   // 시험 실행(-seed·-shots·-load)은 표시를 건드리지 않는다
            track = true;
            bool crashed = PlayerPrefs.GetInt("running", 0) == 1;
            if (crashed) ErrLog("지난 실행이 갑자기 꺼짐");
            Running(true);
            return crashed;
        }

        void Update()
        {
            Draw.ui = Application.isMobilePlatform ? Mathf.Clamp(Screen.dpi / 150f, 1, 3) : Mathf.Clamp(Screen.height / 1000f, 1, 2);
            W = Screen.width / Draw.ui; H = Screen.height / Draw.ui;
            Layout();
            while (run.days.TryDequeue(out var d)) hist.Add(d);
            while (run.news.TryDequeue(out var e)) { news.Add(e); if (news.Count > 200) news.RemoveAt(0); }
            snap = run.snap; bool fresh = snap != lastSnap; lastSnap = snap;
            if (snap == null) return;
            mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / Draw.ui;
            inView = !showTree && !showPath && !OverPanel(mouse);
            long t = Perf.T(); DiveUpdate(Mathf.Min(Time.deltaTime, 0.1f), fresh); Perf.Add(0, t); Perf.Frame(avs.Count, snap.day, run?.pl);
        }

        void Layout()
        {
            topH = showBar ? 46 : 24;
            float x = showSide ? W - panelW - 10 : W + 10;   // 접힌 오른쪽 판은 화면 밖(그래프·계통수·알림이 그만큼 넓어진다)
            mapR = new Rect(x, topH + 8, panelW, 132);
            float ih = sel < 0 ? 0 : infoMore ? INFO_H : INFO_S;
            infoR = new Rect(x, H - 8 - ih, panelW, ih);
            listR = new Rect(x, mapR.yMax + 8, panelW, Mathf.Min(listWant, infoR.y - mapR.yMax - (sel >= 0 ? 16 : 8)));   // 목록이 짧으면 판도 짧게
            graphR = new Rect(10, H - graphH - 8, Mathf.Min(x, W) - 20, graphH);
            gauge = new Rect(10, topH + 8, 46, Mathf.Max(140, (showGraph ? graphR.y : H) - 80 - topH - 8));
        }
        bool OverPanel(Vector2 m) => m.y < topH || mapR.Contains(m) || listR.Contains(m) || infoR.Contains(m) || gauge.Contains(m) || showGraph && graphR.Contains(m) || showEv && evR.Contains(m);

        // 종 목록에서 고르면: 그 종이 가장 많이 사는 칸으로 들어가 몸에 맞는 축척으로 본다
        void Focus(int id)
        {
            sel = id; var s = snap?.sp[id]; if (s?.where == null || s.where.Length == 0) return;
            var p = run.pl; int v = s.where[0];
            if (v >= p.NF0) DiveTo(v - p.NF0, p.zTop[v] + p.thick[v] * 0.5, s);
            else if (v >= p.NV0) DiveTo(p.ventReg[v - p.NV0], p.zTop[v] + p.thick[v] * 0.5, s);
            else DiveTo(v / Cfg.NY, p.zTop[v] + Math.Min(p.thick[v] * 0.5, 8), s);
        }

    }

    // -perf: 5초마다 프레임 시간(평균·최대)과 부분별 시간·GC 횟수를 로그로(렉 찾기). 안 켜면 아무것도 안 함
    static class Perf
    {
        public static readonly bool On = Array.IndexOf(Environment.GetCommandLineArgs(), "-perf") >= 0;
        static readonly string[] N = { "물속", "맞추기", "움직임", "그리기", "고르기", "판", "몸그물", "사진" };
        static readonly double[] sum = new double[8], max = new double[8]; static readonly int[] cnt = new int[8];
        static double fSum, fMax; static int frames, gc0; static float t0;
        public static long T() => On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        public static void Add(int k, long from)
        {
            if (!On) return; double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - from) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            lock (sum) { sum[k] += ms; max[k] = Math.Max(max[k], ms); cnt[k]++; }
        }
        static double day0 = -1; static readonly double[] st0 = new double[5], ev0 = new double[4]; static long tk0;
        public static void Frame(int avs, double day, Life.Planet pl = null)   // day: 지구 날짜(시뮬 속도 = 5초 동안 간 날 수), pl: 틱마다 단계 시간(시험기 PROF와 비교: 같은 일인데 앱이 느린 게 계산 탓인지)
        {
            if (!On) return; double f = Time.unscaledDeltaTime * 1000; fSum += f; fMax = Math.Max(fMax, f); frames++;
            if (Time.unscaledTime - t0 < 5) return;
            var sb = new System.Text.StringBuilder($"[EvoTank] 성능 {frames / (Time.unscaledTime - t0):0} fps · 시뮬 {(day0 < 0 ? 0 : (day - day0) / (Time.unscaledTime - t0)):0.00}일/초 · 프레임 평균 {fSum / frames:0.0} 최대 {fMax:0} ms · GC {GC.CollectionCount(0) - gc0}회 · 개체 {avs} |");
            lock (sum) for (int k = 0; k < N.Length; k++) { if (cnt[k] > 0) sb.Append($" {N[k]} {sum[k] / frames:0.00}/{max[k]:0.0}ms×{cnt[k]}"); sum[k] = max[k] = 0; cnt[k] = 0; }
            if (pl != null) { long dt = pl.tick - tk0; var ts = pl.tStage; if (dt > 0 && tk0 > 0) sb.Append($" | 틱 {(Time.unscaledTime - t0) * 1000 / dt:0.0} ms = 하루 {(ts[0] - st0[0]) * 1000 / dt:0.0} 기후 {(ts[1] - st0[1]) * 1000 / dt:0.0} 생태 {(ts[2] - st0[2]) * 1000 / dt:0.0} 물질 {(ts[3] - st0[3]) * 1000 / dt:0.0} 진화 {(ts[4] - st0[4]) * 1000 / dt:0.0}{(pl.tEvo is double[] e ? $"(고르기 {(e[0] - ev0[0]) * 1000 / dt:0.0} 부모 {(e[1] - ev0[1]) * 1000 / dt:0.0} 후보 {(e[2] - ev0[2]) * 1000 / dt:0.0} 정착 {(e[3] - ev0[3]) * 1000 / dt:0.0})" : "")} + 나머지"); Array.Copy(ts, st0, 5); if (pl.tEvo != null) Array.Copy(pl.tEvo, ev0, 4); tk0 = pl.tick; }
            Debug.Log(sb.ToString()); fSum = fMax = 0; frames = 0; gc0 = GC.CollectionCount(0); t0 = Time.unscaledTime; day0 = day;
        }
    }
}
