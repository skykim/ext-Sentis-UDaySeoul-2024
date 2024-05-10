using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UI;

/*
 *  YOLO inference script
 *  =====================
 * 
 * Place this script on the Main Camera.
 * 
 * Place the yolov7-tiny.sentis file and a *.mp4 video file in the Assets/StreamingAssets folder
 * 
 */


public class RunYOLO : MonoBehaviour
{
    const string modelName = "yolov7-tiny.sentis";    
    // Link the classes.txt here:
    public TextAsset labelsAsset;
    // Create a Raw Image in the scene and link it here:
    public RawImage displayImage;
    // Link to a bounding box texture here:
    public Sprite boxTexture;
    // Link to the font for the labels:
    public Font font;
    public Camera _excavatorCam;

    private Transform displayLocation;
    private Model model;
    private IWorker engine;
    private string[] labels;
    private RenderTexture targetRT;
    RenderTexture activeRT;
    public RenderTexture TT;
    const BackendType backend = BackendType.GPUCompute;

    //Image size for the model
    private const int imageWidth = 640;
    private const int imageHeight = 640;


    //private VideoPlayer video;

    List<GameObject> boxPool = new List<GameObject>();
    //bounding box data
    public struct BoundingBox
    {
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public string label;
        public float confidence;
    }
    
    void Start()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        
        //Parse neural net labels
        labels = labelsAsset.text.Split('\n');

        //Load model
        model = ModelLoader.Load(Application.streamingAssetsPath +"/"+ modelName);

        targetRT = new RenderTexture(imageWidth, imageHeight, 0);
        //Create image to display video
        displayLocation = displayImage.transform;

        //Create engine to run model
        engine = WorkerFactory.CreateWorker(backend, model);
        activeRT = new RenderTexture(1920, 1080, 0);
    }

    private void Update()
    {
        ExecuteML();
        //TextureValidation();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
    
    void TextureValidation()
    {
        UnityEngine.Profiling.Profiler.BeginSample("Copy Textures");

     
        Graphics.Blit(TT, targetRT);
        displayImage.texture = TT;
        
        //Graphics.Blit(_excavatorCam.targetTexture, activeRT);
        //displayImage.texture = activeRT;
        UnityEngine.Profiling.Profiler.EndSample();
    }

    public void ExecuteML()
    {
        
        TextureValidation();
        ClearAnnotations();
        UnityEngine.Profiling.Profiler.BeginSample("Execute Yolo");

        using var input = TextureConverter.ToTensor(targetRT, imageWidth, imageHeight, 3);
        engine.Execute(input);
        //Debug.Log("Run! Model");
        var output = engine.PeekOutput() as TensorFloat;
        output.CompleteOperationsAndDownload();
        output.PrintDataPart(7);
        float displayWidth = displayImage.rectTransform.rect.width;
        float displayHeight = displayImage.rectTransform.rect.height;

        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;

        UnityEngine.Profiling.Profiler.EndSample();


        UnityEngine.Profiling.Profiler.BeginSample("DrawingBox");
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
                label = labels[(int)output[n, 5]],
                confidence = Mathf.FloorToInt(output[n, 6] * 100 + 0.5f)
            };
            //Debug.Log("Set box");
            DrawBox(box, n); 
        }
        UnityEngine.Profiling.Profiler.EndSample();
        input.Dispose();
        output.Dispose();
    }

    public void DrawBox(BoundingBox box , int id)
    {
        //Debug.Log("Drawing Box");
        //Create the bounding box graphic or get from pool
        GameObject panel;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
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
        panel.transform.SetParent(displayLocation, false);

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

        boxPool.Add(panel);
        return panel;
        
    }

    public void ClearAnnotations()
    {
        UnityEngine.Profiling.Profiler.BeginSample("ClearAnnotations");

        foreach(var box in boxPool)
        {
            box.SetActive(false);
        }
        UnityEngine.Profiling.Profiler.EndSample();
    }

    private void OnDestroy()
    {
        engine?.Dispose();
    }
}
