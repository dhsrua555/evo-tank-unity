// 5단계 — 점검 사진: EvoTank.x86_64 -shots <폴더> [-days N] [-seed S] → 최대 속도로 N일(기본 1500)까지 간 뒤
// 전체 · 가장 많은 종 확대 · 계통수 · 그래프 끔을 찍고 끝낸다. 로그 줄은 "[EvoTank] …".
using System;
using System.Collections;
using System.IO;
using System.Linq;
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
            yield return Shot(dir, "1_전체");
            if (snap.alive.Length > 0)
            {
                int big = snap.alive.OrderBy(i => -snap.sp[i].biomass).First();
                open.Add(snap.sp[big].clade);
                Focus(big); yield return new WaitForSeconds(1); yield return Shot(dir, "2_가장많은종");
                zoom = 12; Clamp(); yield return new WaitForSeconds(0.5f); yield return Shot(dir, "3_크게");
            }
            zoom = 1; Clamp();
            showTree = true; yield return new WaitForSeconds(0.5f); yield return Shot(dir, "4_계통수"); showTree = false;
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
