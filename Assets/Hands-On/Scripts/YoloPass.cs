using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

class YoloPass : CustomPass
{
    public RenderTexture targetRT;
    protected override void Execute(CustomPassContext ctx)
    {
        Graphics.Blit(ctx.cameraColorBuffer, targetRT);
    }
}