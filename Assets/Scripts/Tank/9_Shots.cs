// 9단계 — 점검 사진: EvoTank.x86_64 -shots <폴더> [-days N] [-seed S] → 최대 속도로 N일(기본 1500)까지 간 뒤
// 처음 화면 · 가장 많은 종 따라가기 · 그 종 크게 · 둘째 종 · 가장 얕은 바다 1.25 m(물속 20 cm 시야) · 표층 현미경 · 계통수 · 사건 메뉴 · 내린 종 목록 · 진화 과정(큰 변화만·전체)을 찍고 끝낸다. 로그 줄은 "[EvoTank] …".
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Life;
using UnityEngine;

namespace Tank
{
    public sealed partial class Tank
    {
        IEnumerator Shots()
        {
            var a = Environment.GetCommandLineArgs(); string dir = a[Array.IndexOf(a, "-shots") + 1];
            int days = Arg("-days", 1500);
            Directory.CreateDirectory(dir);
            run.speed = 0; run.paused = false;
            float t0 = Time.realtimeSinceStartup;
            while (snap == null || snap.day < days) { yield return new WaitForSeconds(1); if (Time.realtimeSinceStartup - t0 > 1800) break; }
            run.paused = true; yield return new WaitForSeconds(1.5f);
            Debug.Log($"[EvoTank] {snap.day:0}일까지 {Time.realtimeSinceStartup - t0:0}초, 종 {snap.nSp}, 계통 {snap.nStrain}, O2 {snap.O2:0.0e0} PAL, 기온 {snap.T:0.0}");
            StartSpot();   // 앱을 막 켠 것처럼: 지금 가장 많은 종이 있는 곳에서 시작(처음 0일에 고른 곳은 그 사이 비었을 수 있다)
            yield return new WaitForSeconds(2); yield return Shot(dir, "1_처음");
            if (snap.alive.Length > 0)
            {
                int big = snap.alive.OrderBy(i => -snap.sp[i].biomass).First();
                Focus(big); yield return new WaitForSeconds(5); yield return Shot(dir, "2_따라가기");
                Debug.Log($"[EvoTank] 물속 {run.pl.geo.Name(reg)} 수심 {py:0.0} m 시야 {LenTxt(B)} 보이는 {avs.Count(x => !x.dying)}마리 농축 {F:0.##e0} 따라가기 {(selAv != null ? snap.sp[selAv.sp].name : "없음")} 바다눈 {snap.vPOC[Voxel()]:0.###}");
                Bt *= 0.6f; yield return new WaitForSeconds(3); yield return Shot(dir, "3_크게");
                int big2 = snap.alive.OrderBy(i => -snap.sp[i].biomass).Skip(1).FirstOrDefault();
                if (big2 != big) { Focus(big2); yield return new WaitForSeconds(5); yield return Shot(dir, "4_둘째종"); }
            }
            var g = run.pl.geo; int r0 = 0; for (int c = 1; c < g.NO; c++) if (g.rDepth[c] < g.rDepth[r0]) r0 = c;
            sel = -1; GoTo(r0, 1.25); Bt = B = 0.2f; yield return new WaitForSeconds(3); yield return Shot(dir, "5_얕은바다");
            Debug.Log($"[EvoTank] 얕은 바다 {g.Name(r0)} 바닥 {g.rDepth[r0]:0} m 보이는 {avs.Count(x => !x.dying)}마리 점 {smallN:0.#e0} 바다눈 {snap.vPOC[Voxel()]:0.###}");
            GoTo(r0, 5); int vs = Voxel(), dense = -1; foreach (int i in snap.alive) if (snap.sp[i].body != null && (dense < 0 || snap.sp[i].conc[vs] > snap.sp[dense].conc[vs])) dense = i;
            Bt = B = dense >= 0 ? Mathf.Clamp(Len(snap.sp[dense]) * 12, 5e-6f, 3) : 3e-4f; yield return new WaitForSeconds(3); yield return Shot(dir, "6_표층현미경");
            Debug.Log($"[EvoTank] 표층 현미경: 가장 짙은 종 {(dense >= 0 ? snap.sp[dense].name + " " + Size(snap.sp[dense].body.size) : "없음")} 시야 {LenTxt(B)} 보이는 {avs.Count(x => !x.dying)}마리");
            showTree = true; yield return new WaitForSeconds(0.5f); yield return Shot(dir, "7_계통수"); showTree = false;
            // 태블릿 점검: 사건 메뉴(설명·지금 광합성을 막는 것) · 아래로 내린 종 목록
            showEv = true; yield return new WaitForSeconds(0.5f); yield return Shot(dir, "8_사건메뉴"); showEv = false;
            listScroll.y = 1e5f; yield return new WaitForSeconds(0.5f); yield return Shot(dir, "9_목록아래");
            // 진화 과정: 조상 줄이 가장 긴 살아 있는 종
            if (snap.alive.Length > 0)
            {
                int Depth(int i) { int d = 0; for (int q = i; q >= 0; q = snap.sp[q].parent) d++; return d; }
                int deep = snap.alive.OrderBy(i => -Depth(i)).First(); sel = deep; showPath = true; pathTip = deep; pathScroll = 0; pathBig = true;
                yield return new WaitForSeconds(1.5f); yield return Shot(dir, "10_진화과정");
                Debug.Log($"[EvoTank] 진화 과정 {snap.sp[deep].name}: 조상 {pathFull.Length}종, 큰 변화 {pathIds.Length}");
                pathBig = false; yield return new WaitForSeconds(1.5f); yield return Shot(dir, "11_진화과정_전체"); showPath = false;
            }
            Application.Quit();
        }

        IEnumerator Shot(string dir, string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            Debug.Log($"[EvoTank] shot {name}");
            yield return null;
        }
    }
}
