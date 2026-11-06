// 보는 느낌: 물속(mm 이상) = 방향마다 다른 물빛 배경·스넬 창·빛물결·빛줄기·수면, 현미경(mm 미만) = 어두운 시야·얕은 초점.
// 피사계 심도: 생물 몸의 카메라 거리를 따로 그려(Depth.shader) 뒤처리(Post.shader)에서 초점면 밖을 흐린다. 떠다니는 입자는 제 셰이더가 스스로 퍼진다
using UnityEngine;
using UnityEngine.Rendering;

public partial class EvoTank
{
    Material mDepth, mPost, mSky, mSurf; CommandBuffer cbD; RenderTexture depthRT; Mesh surfM;

    void LookInit()
    {
        mDepth = new Material(Shader.Find("Evo/Depth")); mPost = new Material(Shader.Find("Evo/Post"));
        mSky = new Material(Shader.Find("Evo/Sky")); mSurf = new Material(Shader.Find("Evo/Surface"));
        cbD = new CommandBuffer { name = "evo depth" }; cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, cbD);
        cam.gameObject.AddComponent<PostFX>().mat = mPost;
        RenderSettings.skybox = mSky;
        surfM = new Mesh { vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
        surfM.RecalculateBounds();
    }

    // Draw 처음: 몸 거리 그림을 비운다(0 = 몸 없음)
    void LookBegin()
    {
        if (!depthRT || depthRT.width != Screen.width || depthRT.height != Screen.height)
        {
            if (depthRT) depthRT.Release();
            depthRT = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.RHalf) { name = "evo depth" }; depthRT.Create();
            mPost.SetTexture("_DepthTex", depthRT);
        }
        cbD.Clear(); cbD.SetRenderTarget(depthRT); cbD.ClearRenderTarget(true, true, Color.clear);
    }
    void LookBody(Mesh mesh, Matrix4x4 m, MaterialPropertyBlock p) => cbD.DrawMesh(mesh, m, mDepth, 0, 0, p);

    // Draw 끝: 배경·수면·빛물결·빛줄기·초점 값
    void LookEnd(bool ms, float u, float sun)
    {
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
        if (ms && yS < H * 1.3f) Graphics.DrawMesh(surfM, Matrix4x4.TRS(new Vector3(0, yS, 0), Quaternion.identity, Vector3.one * BOX * 24), mSurf, 0);   // 수면이 보이면
    }
}
