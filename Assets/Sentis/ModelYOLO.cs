using System;
using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

public class ModelYOLO : MonoBehaviour
{
    [SerializeField]
    public enum SelectedModel
    {
        Original,
        Float16,
        Uint8
    }
    public SelectedModel _selectedModel;

    string modelName;
    public TextAsset labelsAsset;
    public Sprite boxTexture;
    public Font font;
    public GameObject displayLocation;
    
    private Model model;
    public IWorker engine;
    const BackendType backend = BackendType.GPUCompute;
    private CustomPassVolume customPassVolume;
    
    private string[] labels;
    private const int imageWidth = 640;
    private const int imageHeight = 640;

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
        customPassVolume = GetComponent<CustomPassVolume>();
        labels = labelsAsset.text.Split('\n');
    }

    public void SelectingModel()
    {
        engine?.Dispose();
        switch (_selectedModel)
        {
            case SelectedModel.Original:
                modelName = "yolov7-tiny.sentis";
                break;
            case SelectedModel.Float16:
                modelName = "yolov7-tiny_Float16.sentis";
                break;
            case SelectedModel.Uint8:
                modelName = "yolov7-tiny_Uint8.sentis";
                break;
        }
        model = ModelLoader.Load(Application.streamingAssetsPath +"/"+ modelName);
        engine = WorkerFactory.CreateWorker(backend, model);
        Debug.Log("Select : " + modelName);
    }

    void OnValidate()
    {
        SelectingModel();
    }

    public void StartYolo()
    {
        customPassVolume.enabled = true;
    }

    public void StopYolo()
    {
        ClearAnnotations();
        customPassVolume.enabled = false;
    }

    public void ToggleYolo()
    {
        if (customPassVolume.enabled)
            StopYolo();
        else
            StartYolo();
    }

    public void DrawBoundingBoxes(int screenWidth, int screenHeight, TensorFloat output)
    {
        ClearAnnotations();
        
        float displayWidth = screenWidth;
        float displayHeight = screenHeight;

        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;

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
            DrawBox(box, n); 
        }
    }
    
    public void DrawBox(BoundingBox box , int id)
    {
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

        boxPool.Add(panel);
        return panel;
    }

    public void ClearAnnotations()
    {
        foreach(var box in boxPool)
        {
            box.SetActive(false);
        }
    }
    
    private void OnDestroy()
    {
        engine?.Dispose();
    }
}