using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// the tilt-shift blur as a render pass queued from code onto the battle camera each frame, so no renderer asset is
// edited. runs before post-processing, so the colour grade and vignette land on top of the blurred edges.
public class TiltShiftPass : ScriptableRenderPass
{
    private readonly Material mat;

    public TiltShiftPass(Material material)
    {
        mat = material;
        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        requiresIntermediateTexture = true;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (mat == null) return;
        var res = frameData.Get<UniversalResourceData>();
        if (res.isActiveTargetBackBuffer) return;
        var src = res.activeColorTexture;
        var desc = renderGraph.GetTextureDesc(src);
        desc.name = "TiltShift"; desc.clearBuffer = false;
        var dst = renderGraph.CreateTexture(desc);
        renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(src, dst, mat, 0), "TiltShift");
        res.cameraColor = dst;
    }
}
