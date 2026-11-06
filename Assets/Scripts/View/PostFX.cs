using UnityEngine;
// 카메라에 붙는 화면 뒤처리(피사계 심도·빛줄기·가장자리, Post.shader). 값은 EvoTank가 매 프레임 넣는다(Look.cs)
public sealed class PostFX : MonoBehaviour
{
    public Material mat;
    void OnRenderImage(RenderTexture src, RenderTexture dst) { if (mat) Graphics.Blit(src, dst, mat); else Graphics.Blit(src, dst); }
}
