using Unity.Sentis;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

class YoloPass : CustomPass
{
    private RenderTexture targetRT;
    private const int imageWidth = 640;
    private const int imageHeight = 640;
    public ModelYOLO yoloObject;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        yoloObject.ClearAnnotations();
    }

    protected override void Execute(CustomPassContext ctx)
    {
        targetRT = new RenderTexture(imageWidth, imageHeight, 0);
        Graphics.Blit(ctx.cameraColorBuffer, targetRT);
        
        using var input = TextureConverter.ToTensor(targetRT, imageWidth, imageHeight, 3);
        yoloObject.engine.Execute(input);
        using var output = yoloObject.engine.PeekOutput() as TensorFloat;
        output.CompleteOperationsAndDownload();
        //output.PrintDataPart(7);
        
        yoloObject.DrawBoundingBoxes(ctx.cameraColorBuffer.rt.width, ctx.cameraColorBuffer.rt.height, output);
    }

    protected override void Cleanup()
    {
        yoloObject.ClearAnnotations();
    }
}