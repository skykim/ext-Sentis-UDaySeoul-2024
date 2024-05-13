using Unity.Sentis;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

class YoloPass : CustomPass
{
    private RenderTexture _targetRT;
    private const int _imageWidth = 640;
    private const int _imageHeight = 640;
    public ModelYOLO yoloObject;
        
    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        yoloObject.ClearAnnotations();
        _targetRT = new RenderTexture(_imageWidth, _imageHeight, 0);
    }

    protected override void Execute(CustomPassContext ctx)
    {
        Graphics.Blit(ctx.cameraColorBuffer, _targetRT);
        
        using var input = TextureConverter.ToTensor(_targetRT, _imageWidth, _imageHeight, 3);
        yoloObject.engine.Execute(input);
        using var output = yoloObject.engine.PeekOutput() as TensorFloat;
        output.CompleteOperationsAndDownload();
        
        yoloObject.DrawBoundingBoxes(ctx.cameraColorBuffer.rt.width, ctx.cameraColorBuffer.rt.height, output);
    }

    protected override void Cleanup()
    {
        yoloObject.ClearAnnotations();

        if (_targetRT != null)
        {
            _targetRT.Release();
            _targetRT = null;
        }
    }
}