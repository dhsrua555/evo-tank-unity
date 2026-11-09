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
            Application.targetFrameRate = 30;            // 화면은 30 fps면 충분: 남는 CPU는 시뮬레이션에
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            cam = GetComponent<Camera>(); cam.fieldOfView = 50;
            // 시드나 사진(-shots)을 준 시험 실행은 저장 파일을 읽지도 쓰지도 않는다(사용자 지구를 덮지 않게)
            bool shots = Array.IndexOf(Environment.GetCommandLineArgs(), "-shots") >= 0, fresh = newEarth; newEarth = false;
            int seedArg = Arg("-seed", -1); bool test = seedArg >= 0 || shots;
            string path = test ? null : Path.Combine(Application.persistentDataPath, "earth.sav");
            Planet pl = null; Life.Save.Head h = null;
            if (path != null && !fresh && File.Exists(path))
                try { pl = Life.Save.Read(path, out h); Note($"저장한 지구를 이어서 돌립니다 · {h.day:0}일"); }
                catch (Exception e)
                {
                    Debug.LogWarning("[EvoTank] 저장 파일을 못 읽음: " + e.Message);
                    try { File.Copy(path, path + ".old", true); } catch { }
                    Note("저장 파일을 못 읽어 새 지구로 시작합니다(옛 파일은 earth.sav.old)");
                }
            pl ??= new Planet(seedArg >= 0 && !fresh ? seedArg : Environment.TickCount & 0xffff);
            run = new Runner(pl, path);
            // 태블릿: 앱이 꺼져 있던 동안 갔어야 할 날을 따라잡는다(내렸다 다시 열 때와 같은 규칙)
            if (h != null && Application.isMobilePlatform) run.ffDays = Math.Clamp((DateTime.UtcNow - h.at).TotalSeconds * h.rate, 0, 30000);
            Fonts(); LookInit();
            if (shots) StartCoroutine(Shots());
            Debug.Log($"[EvoTank] 시작: 시드 {pl.seed}, {pl.Day:0}일, 해역 {pl.NR}, 칸 {pl.V}, 저장 {path ?? "안 함"}");
        }

        void OnDestroy() => run?.Stop();
        void OnApplicationQuit() { run?.Stop(); run?.SaveNow(); }
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

        // 태블릿: 앱이 멈춘 동안 갔어야 할 날을 다시 열 때 따라잡는다(최대 3만 일)
        DateTime pausedAt; double pausedDay, pausedRate;
        void OnApplicationPause(bool p)
        {
            if (run == null) return;
            if (p) { pausedAt = DateTime.UtcNow; pausedDay = run.pl.Day; pausedRate = run.CatchRate; run.SaveNow(); return; }   // 내리면 바로 저장: 그대로 꺼져도 이어진다
            if (run.paused || pausedAt == default) return;
            double want = (DateTime.UtcNow - pausedAt).TotalSeconds * pausedRate, done = run.pl.Day - pausedDay;
            run.ffDays = Math.Clamp(want - done, 0, 30000);
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
            inView = !showTree && !OverPanel(mouse);
            // 따라잡는 동안은 3D를 멈추고 화면을 10 fps로: 남는 CPU를 시뮬레이션에
            bool ff = run.ffDays > 0;
            Application.targetFrameRate = ff ? 10 : 30; cam.cullingMask = ff ? 0 : ~0;
            if (!ff) DiveUpdate(Mathf.Min(Time.deltaTime, 0.1f), fresh);
        }

        void Layout()
        {
            float x = W - panelW - 10;
            mapR = new Rect(x, topH + 8, panelW, 132);
            float ih = sel < 0 ? 0 : infoMore ? INFO_H : INFO_S;
            infoR = new Rect(x, H - 8 - ih, panelW, ih);
            listR = new Rect(x, mapR.yMax + 8, panelW, Mathf.Min(listWant, infoR.y - mapR.yMax - (sel >= 0 ? 16 : 8)));   // 목록이 짧으면 판도 짧게
            graphR = new Rect(10, H - graphH - 8, x - 20, graphH);
            gauge = new Rect(10, topH + 8, 46, Mathf.Max(140, (showGraph ? graphR.y : H) - 80 - topH - 8));
        }
        bool OverPanel(Vector2 m) => m.y < topH || mapR.Contains(m) || listR.Contains(m) || infoR.Contains(m) || gauge.Contains(m) || showGraph && graphR.Contains(m) || showEv && evR.Contains(m);

        // 종 목록에서 고르면: 그 종이 가장 많이 사는 칸으로 들어가 몸에 맞는 축척으로 본다
        void Focus(int id)
        {
            sel = id; var s = snap?.sp[id]; if (s?.where == null || s.where.Length == 0) return;
            var p = run.pl; int v = s.where[0];
            if (v >= p.NV0) DiveTo(p.ventReg[v - p.NV0], p.zTop[v] + p.thick[v] * 0.5, s);
            else DiveTo(v / Cfg.NY, p.zTop[v] + Math.Min(p.thick[v] * 0.5, 8), s);
        }

    }
}
