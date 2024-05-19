using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

class CustomYoloPass : CustomPass
{
    public RenderTexture targetRT;
    protected override void Execute(CustomPassContext ctx)
    {
        Graphics.Blit(ctx.cameraColorBuffer, targetRT);
    }
}