// 5단계 — 물속 보는 느낌(예전 v0.1 화면 그대로): 물빛·배경·떠다니는 입자·바닥·수면·피사계 심도.
// 시야가 2 mm보다 크면 물속(방향마다 다른 물빛 배경, 스넬 창·빛물결·빛줄기, 멀리가 푸르게 흐림), 작으면 암시야 현미경(어두운 시야, 막 가장자리가 빛남, 얕은 초점).
// 물빛 = 이 깊이까지 내려온 햇빛 색(빨강이 먼저 줄어 깊을수록 청록) + 그 칸의 철(녹색)·황화수소(흐린 자주). 바다눈 양 = 그 칸의 실제 사체 입자(POC) 농도.
// 피사계 심도: 생물 몸의 카메라 거리를 따로 그려(Depth.shader) 뒤처리(Post.shader)에서 초점면 밖을 흐린다. 떠다니는 입자는 제 셰이더가 스스로 퍼진다.
using System;
using Life;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tank
{
    // 카메라 뒤처리: 그린 화면을 Post 셰이더로 한 번 거른다
    public sealed class PostFX : MonoBehaviour
    {
        public Material mat;
        void OnRenderImage(RenderTexture src, RenderTexture dst) { if (mat) Graphics.Blit(src, dst, mat); else Graphics.Blit(src, dst); }
    }

    public sealed partial class Tank
    {
        static readonly Vector3 SIGMA = new(0.45f, 0.11f, 0.08f);   // 맑은 바닷물 빛 감쇠(/m)
        static readonly float[] SNOW_P = { 2.5e-5f, 2e-4f, 1.6e-3f, 1.28e-2f, 0.1024f, 0.8192f };   // 떠다니는 입자 무늬 주기(m, 입자 반지름 = 주기/400: 콜로이드 60 nm … 바다눈 2 mm). 시야를 덮을 만큼 타일로 깐다
        static readonly float[] SNOW_SINK = { 1e-9f, 3e-8f, 1e-6f, 3e-5f, 1e-3f, 1e-2f };   // 가라앉는 속도(m/s, 스토크스: 반지름²에 비례)
        Material mMicro, mMacro, mSky, mDepth, mPost, mSurf, mSnow, mDots, mFloor;
        CommandBuffer cbD; RenderTexture depthRT; Mesh surfM, snowM, dotsM, floorM;
        MaterialPropertyBlock mpb, mpbS;
        float sunNow;

        void LookInit()
        {
            mMicro = new Material(Shader.Find("Evo/Micro")); mMacro = new Material(Shader.Find("Evo/Macro")); mSky = new Material(Shader.Find("Evo/Sky"));
            mDepth = new Material(Shader.Find("Evo/Depth")); mPost = new Material(Shader.Find("Evo/Post")); mSurf = new Material(Shader.Find("Evo/Surface"));
            mSnow = new Material(Shader.Find("Evo/Snow")); mDots = new Material(Shader.Find("Evo/Dots")); mFloor = new Material(Shader.Find("Evo/Floor"));
            mpb = new MaterialPropertyBlock(); mpbS = new MaterialPropertyBlock();
            cbD = new CommandBuffer { name = "evo depth" }; cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, cbD);
            cam.gameObject.AddComponent<PostFX>().mat = mPost;
            RenderSettings.skybox = mSky;
            surfM = Quad(0.5f, 0); floorM = Quad(HB, -HB);
            snowM = DotsMesh(600); dotsM = DotsMesh(3000);
        }

        static Mesh Quad(float h, float y)
        {
            var m = new Mesh { vertices = new[] { new Vector3(-h, y, -h), new Vector3(h, y, -h), new Vector3(h, y, h), new Vector3(-h, y, h) }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        // 점 무늬 그물: 네 모서리가 한 점을 나눠 쓰는 사각형(셰이더가 화면을 보게 편다). uv2.x = 순번(_Count보다 크면 안 그린다)
        static Mesh DotsMesh(int n)
        {
            var r = new System.Random(7); var v = new Vector3[n * 4]; var uv = new Vector2[n * 4]; var id = new Vector2[n * 4]; var t = new int[n * 6];
            Vector2[] cr = { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) };
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3((float)r.NextDouble(), (float)r.NextDouble(), (float)r.NextDouble()); float rid = (float)r.NextDouble();
                for (int j = 0; j < 4; j++) { v[i * 4 + j] = p; uv[i * 4 + j] = cr[j]; id[i * 4 + j] = new Vector2(rid, 0); }
                int b = i * 4; t[i * 6] = b; t[i * 6 + 1] = b + 1; t[i * 6 + 2] = b + 2; t[i * 6 + 3] = b; t[i * 6 + 4] = b + 2; t[i * 6 + 5] = b + 3;
            }
            var m = new Mesh { vertices = v, uv = uv, uv2 = id, triangles = t }; m.bounds = new Bounds(Vector3.zero, Vector3.one * 1000); return m;
        }

        // 그리기 처음: 몸 거리 그림을 비우고, 물빛 전역 값을 둔다
        void LookBegin(int v)
        {
            if (!depthRT || depthRT.width != Screen.width || depthRT.height != Screen.height)
            {
                if (depthRT) depthRT.Release();
                depthRT = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.RHalf) { name = "evo depth" }; depthRT.Create();
                mPost.SetTexture("_DepthTex", depthRT);
            }
            cbD.Clear(); cbD.SetRenderTarget(depthRT); cbD.ClearRenderTarget(true, true, Color.clear);
            var s = snap; float u = BOX / B, sun = sunNow = Mathf.Clamp01(s.vLight[v] * 1.6f);
            var lc = new Vector3(Mathf.Exp(-SIGMA.x * (float)py), Mathf.Exp(-SIGMA.y * (float)py), Mathf.Exp(-SIGMA.z * (float)py)); lc /= Mathf.Max(lc.x, Mathf.Max(lc.y, lc.z));
            var water = Color.Lerp(new Color(0.01f, 0.03f, 0.06f), new Color(0.1f + 0.14f * lc.x, 0.42f * lc.y, 0.55f * lc.z), sun);
            float fe = Mathf.Clamp01(Mathf.Log10(1 + s.vFe2[v]) / 2), hs = Mathf.Clamp01(Mathf.Log10(1 + s.vH2S[v]) / 1.5f);
            water = Color.Lerp(water, new Color(0.08f, 0.2f, 0.1f) * (0.3f + sun), fe * 0.35f); water = Color.Lerp(water, new Color(0.25f, 0.2f, 0.3f) * (0.3f + sun), hs * 0.35f);
            int kd = run.pl.geo.rKind[reg]; if (kd > 0) water = Color.Lerp(water, new Color(0.3f, 0.27f, 0.17f) * (0.3f + 0.7f * sun), kd == 2 ? 0.65f : 0.3f);   // 조간대 웅덩이·흙 속 물막: 흙탕 빛
            cam.backgroundColor = Micro ? new Color(0.01f, 0.012f, 0.02f) : water;
            Shader.SetGlobalVector("_FogCol", water); Shader.SetGlobalFloat("_Sun", sun);
            Shader.SetGlobalVector("_Sigma", SIGMA / u); Shader.SetGlobalVector("_LightCol", new Vector4(lc.x, lc.y, lc.z, 1));
        }
        void LookBody(Mesh mesh, Matrix4x4 m, MaterialPropertyBlock p) => cbD.DrawMesh(mesh, m, mDepth, 0, 0, p);

        // 너무 작아 몸을 못 그리는 생물(점, small = 상자 안 실제 마리 수)·바다눈·바닥
        void Particles(int v, double small)
        {
            float u = BOX / B, sun = sunNow; bool ms = !Micro;
            double nDots = ms ? Math.Log10(1 + small) * 400 : Math.Log10(1 + small * F) * 150;   // 로그로(그대로 곱하면 화면이 하얗게 찬다). 현미경은 농축 배율도 곱한다
            mDots.SetFloat("_Count", (float)Math.Min(1, nDots / 3000)); mDots.SetFloat("_Brown", ms ? 0 : 5e-7f * u); mDots.SetFloat("_Box", BOX);
            mDots.SetVector("_Off", new Vector4(dotOff.x - Mathf.Floor(dotOff.x), dotOff.y - Mathf.Floor(dotOff.y), dotOff.z - Mathf.Floor(dotOff.z), 0)); mDots.SetVector("_YLim", new Vector4(yF, yS));
            mDots.SetFloat("_Size", ms ? 0.025f : Mathf.Max(0.02f, 0.6e-6f * u));
            mDots.SetColor("_Color", ms ? new Color(0.7f, 0.9f, 0.7f, 0.35f) : new Color(0.75f, 0.8f, 1f, 0.5f));
            Graphics.DrawMesh(dotsM, Matrix4x4.identity, mDots, 0);
            Snow(u, sun, ms, snap.vPOC[v]);
            if (yF > -HB) Floor(u);
        }

        // 바다눈: 주기 P(m) 무늬를 보이는 구를 덮을 만큼 타일로. 크기도 m 단위라 확대하면 커진다
        void Snow(float u, float sun, bool ms, float poc)
        {
            var col = (ms ? new Color(0.8f, 0.9f, 0.85f) : new Color(0.75f, 0.8f, 0.95f)) * (0.35f + 0.65f * sun);
            float amt = Mathf.Clamp(0.15f + 0.45f * Mathf.Log10(1 + poc), 0.08f, 1.6f);
            float pxk = 2 * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2) / Screen.height;
            for (int i = 0; i < SNOW_P.Length; i++)
            {
                float P = SNOW_P[i], q = Mathf.Log(B / P);
                float wl = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Mathf.Log(0.06f), Mathf.Log(0.2f), q)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Mathf.Log(1.2f), Mathf.Log(2.5f), q)));
                if (wl < 0.01f) continue;
                float Pu = P * u, R = HB;
                var f = new Vector3((float)Frac(px / P), (float)Frac(-py / P), (float)Frac(pz / P));
                mpbS.SetVector("_Par", new Vector4(P / 400 * u, 0.5f * wl * amt, R, pxk));
                mpbS.SetVector("_Mot", new Vector4(SNOW_SINK[i] / P, i <= 2 ? 0.004f : 0.0005f, yF, yS));   // 작은 입자는 브라운 운동으로 더 떤다
                mpbS.SetColor("_Col", col);
                int lo = Mathf.FloorToInt(-R / Pu), hi = Mathf.CeilToInt(R / Pu) + 1;
                for (int a = lo; a < hi; a++) for (int b = lo; b < hi; b++) for (int c = lo; c < hi; c++)
                {
                    var o = (new Vector3(a, b, c) - f) * Pu;
                    var near = new Vector3(Mathf.Clamp(0, o.x, o.x + Pu), Mathf.Clamp(0, o.y, o.y + Pu), Mathf.Clamp(0, o.z, o.z + Pu));
                    if (near.sqrMagnitude > R * R) continue;   // 보이는 구와 안 겹치는 타일
                    mpbS.SetVector("_Off", new Vector4(o.x, o.y, o.z, Pu));
                    Graphics.DrawMesh(snowM, Matrix4x4.identity, mSnow, 0, null, 0, mpbS);
                }
            }
        }
        static double Frac(double x) => x - Math.Floor(x);

        // 바닥: 판 한 장의 실제 크기 T(m)는 시야 B에 가까운 2의 거듭제곱. 무늬 네 겹이 축척을 따라 이어진다(확대해도 튀지 않게)
        void Floor(float u)
        {
            int kd = run.pl.geo.rKind[reg];
            double T = Math.Pow(2, Math.Round(Math.Log(B, 2))); float S = (float)(T * u), sc = S / BOX;
            float ox = -(float)Frac(px / T) * S, oz = -(float)Frac(pz / T) * S;
            double lb = Math.Log(B, 2); int k0 = (int)Math.Floor(lb); float fr = (float)(lb - k0); Vector4 L4 = default, W4 = default, OX = default, OZ = default;
            for (int j = 0; j < 4; j++) { double Lj = Math.Pow(2, k0 - 6 + j); L4[j] = (float)Lj; W4[j] = 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * (j + 1 - fr) / 4), 2); OX[j] = (float)Frac(px / (Lj * 64)); OZ[j] = (float)Frac(pz / (Lj * 64)); }
            mFloor.SetVector("_L", L4); mFloor.SetVector("_W", W4); mFloor.SetVector("_OX", OX); mFloor.SetVector("_OZ", OZ);
            mFloor.SetColor("_Base", kd == 2 ? new Color(0.36f, 0.27f, 0.17f) : new Color(0.62f, 0.55f, 0.42f)); mFloor.SetFloat("_Kind", kd == 2 ? 1 : 0); mFloor.SetFloat("_FR", 2 * S);
            for (int i = -2; i <= 2; i++) for (int j = -2; j <= 2; j++)
                Graphics.DrawMesh(floorM, Matrix4x4.Translate(new Vector3(ox + i * S, yF, oz + j * S)) * Matrix4x4.Scale(new Vector3(sc, sc, sc)) * Matrix4x4.Translate(new Vector3(0, HB, 0)), mFloor, 0);
        }

        // 그리기 끝: 배경·수면·빛물결·빛줄기·초점 값
        void LookEnd()
        {
            bool ms = !Micro; float u = BOX / B, sun = sunNow;
            cbD.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            cam.clearFlags = ms ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            Shader.SetGlobalVector("_Obs", new Vector4((float)px, -(float)py, (float)pz, u));
            float shallow = Mathf.Exp(-(float)py / 2.5f);
            float caus = ms ? sun * shallow * 1.4f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2e-3f, 1e-2f, B)) : 0;   // 빛물결 그물은 수 cm라 mm보다 크게 볼 때부터
            float rays = ms ? sun * Mathf.Exp(-(float)py / 4) * 0.35f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.02f, 0.2f, B)) : 0;   // 빛줄기는 수십 cm 기둥
            Shader.SetGlobalVector("_Sky", new Vector4(ms ? sun * shallow * 0.9f : 0, caus, rays, 0));
            float k = Screen.height / 1080f, pxk = 2 * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2) / Screen.height;
            Shader.SetGlobalVector("_DoF", new Vector4(camDist, (ms ? 7 : 20) * k, 9 * k, pxk));   // 현미경은 초점이 아주 얕다
            mPost.SetFloat("_Micro", ms ? 0 : 1);
            var t = cam.transform; float ty = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2);
            mPost.SetVector("_CamF", t.forward); mPost.SetVector("_CamR", t.right * ty * cam.aspect); mPost.SetVector("_CamU", t.up * ty);
            if (ms && yS < HB * 1.3f) Graphics.DrawMesh(surfM, Matrix4x4.TRS(new Vector3(0, yS, 0), Quaternion.identity, Vector3.one * BOX * 24), mSurf, 0);   // 수면이 보이면
        }
    }
}
