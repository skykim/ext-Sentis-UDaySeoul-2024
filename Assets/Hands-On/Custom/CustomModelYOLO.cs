using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

public class CustomModelYOLO : MonoBehaviour
{
    public string modelName = "yolov7-tiny.sentis";
    public TextAsset labelsAsset;
    public Sprite boxTexture;
    public Font font;
    public GameObject displayLocation;
    public RenderTexture yoloTexture;
    
    private Model _model;
    private IWorker _engine;
    const BackendType BACKEND = BackendType.GPUCompute;
    private CustomPassVolume _customPassVolume;
    
    private string[] _labels;
    private const int _imageWidth = 640;
    private const int _imageHeight = 640;

    private List<GameObject> _boxPool = new List<GameObject>();
    
    void Start()
    {
        _customPassVolume = GetComponent<CustomPassVolume>();
        _labels = labelsAsset.text.Split('\n');
        
        _model = ModelLoader.Load(Application.streamingAssetsPath +"/"+ modelName);
        _engine = WorkerFactory.CreateWorker(BACKEND, _model);
    }

    private void LateUpdate()
    {
        if (_customPassVolume.enabled)
        {
            using var input = TextureConverter.ToTensor(yoloTexture, _imageWidth, _imageHeight, 3);
            _engine.Execute(input);
            
            using TensorFloat outputTensor = _engine.PeekOutput() as TensorFloat;
            outputTensor.CompleteOperationsAndDownload();
            DrawBoundingBoxes(yoloTexture.width, yoloTexture.height, outputTensor);
        }
    }

    public void StartYolo()
    {
        _customPassVolume.enabled = true;
    }

    public void StopYolo()
    {
        ClearAnnotations();
        _customPassVolume.enabled = false;
    }

    public void ToggleYolo()
    {
        if (_customPassVolume.enabled)
            StopYolo();
        else
            StartYolo();
    }

    public void DrawBoundingBoxes(int screenWidth, int screenHeight, TensorFloat output)
    {
        float displayWidth = screenWidth;
        float displayHeight = screenHeight;

        float scaleX = displayWidth / _imageWidth;
        float scaleY = displayHeight / _imageHeight;
        
        ClearAnnotations();
        
        //Draw the bounding boxes
        for (int n = 0; n < output.shape[0]; n++)
        {
            if (output[n, 5] != 0f)
            {
                //Debug.Log("Human isn't detected");
                return;
            }

            var box = new BoundingBox
            {
                centerX = ((output[n, 1] + output[n, 3])*scaleX - displayWidth) / 2,
                centerY = ((output[n, 2] + output[n, 4])*scaleY - displayHeight) / 2,
                width = (output[n, 3] - output[n, 1])*scaleX,
                height = (output[n, 4] - output[n, 2])*scaleY,
                label = _labels[(int)output[n, 5]],
                confidence = Mathf.FloorToInt(output[n, 6] * 100 + 0.5f)
            };
            DrawBox(box, n); 
        }
    }
    
    public void DrawBox(BoundingBox box , int id)
    {
        //Create the bounding box graphic or get from pool
        GameObject panel;
        if (id < _boxPool.Count)
        {
            panel = _boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox(Color.yellow);
        }
        
        //Set box position
        panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);

        //Set box size
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.width, box.height);
        
        //Set label text
        var label = panel.GetComponentInChildren<Text>();
        label.text = box.label + " (" + box.confidence + "%)";
    }

    public GameObject CreateNewBox(Color color)
    {
        //Create the box and set image
        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        Image img = panel.AddComponent<Image>();
        img.color = color;
        img.sprite = boxTexture;
        img.type = Image.Type.Sliced;
        panel.transform.SetParent(displayLocation.transform, false);

        //Create the label
        var text = new GameObject("ObjectLabel");
        text.AddComponent<CanvasRenderer>();
        text.transform.SetParent(panel.transform, false);
        Text txt = text.AddComponent<Text>();
        txt.font = font;
        txt.color = color;
        txt.fontSize = 40;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        RectTransform rt2 = text.GetComponent<RectTransform>();
        rt2.offsetMin = new Vector2(20, rt2.offsetMin.y);
        rt2.offsetMax = new Vector2(0, rt2.offsetMax.y);
        rt2.offsetMin = new Vector2(rt2.offsetMin.x, 0);
        rt2.offsetMax = new Vector2(rt2.offsetMax.x, 30);
        rt2.anchorMin = new Vector2(0, 0);
        rt2.anchorMax = new Vector2(1, 1);

        _boxPool.Add(panel);
        return panel;
    }

    public void ClearAnnotations()
    {
        foreach(var box in _boxPool)
        {
            box.SetActive(false);
        }
    }
    
    private void OnDestroy()
    {
        _engine?.Dispose();
    }
}