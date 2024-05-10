using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Sentis;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine.Playables;
using UnityWarehouseSceneHDRP;
using JetBrains.Annotations;
using StarterAssets;
using Unity.VisualScripting;
using UnityEngine.Networking;
using FF = Unity.Sentis.Functional;

/*
 *              Tiny Stories Inference Code
 *              ===========================
 *  
 *  Put this script on the Main Camera
 *  
 *  In Assets/StreamingAssets put:
 *  
 *  MiniLMv6.sentis
 *  vocab.txt
 * 
 *  Install package com.unity.sentis
 * 
 */


public class MiniLM : MonoBehaviour
{
    const BackendType backend = BackendType.GPUCompute;
    public List<string> actionList;
    public List<GameObject> Cameras = new List<GameObject>(); 
    enum CameraState
    {
        ForkreitView,
        NPCView,
        TopView,
        YoloView
    }
    enum YoloState
    {
        On,
        Off
    }
    Dictionary<int, GameObject> cameraDictionary = new Dictionary<int, GameObject>();
    int activatedCamIndex;

    public List<GameObject> _interiorOjbects = new List<GameObject>(); 
    public List<Material> _interiorMaterials= new List<Material>(); 
    bool _modernTheme;    
    public GameObject textYoloMode;
    GameObject _player;
    public GameObject yoloView;
    public GameObject yoloMode;
    public GameObject _reporter; 

    public TMP_Text textSimilarity;


    //string action1 = "go to the site A";
    public PlayableDirector playableDirector;

    //Special tokens
    const int START_TOKEN = 101; 
    const int END_TOKEN = 102;
    
    //Store the vocabulary
    string[] tokens;
    const int FEATURES = 384; //size of feature space
    IWorker engine, dotScore;
    
    [SerializeField]
    private TMP_InputField promptField;
    ThirdPersonController _controller;
    WorkerAgent _reportController;

    void Start()
    {
        //Application.targetFrameRate = 60;
        //_reportController.StopAllCoroutines();

        tokens = File.ReadAllLines(Application.streamingAssetsPath + "/vocab.txt");

        engine = CreateMLModel();
        dotScore = CreateDotScoreModel();
        
        promptField.gameObject.SetActive(false);
        _player = GameObject.FindWithTag("Player");
        _reporter = GameObject.FindWithTag("Reporter");
        _controller = _player.GetComponent<ThirdPersonController>();
        _reportController =  _reporter.GetComponent<WorkerAgent>();

        for (int i = 0; i < Cameras.Count; i++)
        {
            cameraDictionary.Add(i, Cameras[i]);
        }
    }
    
    IWorker CreateMLModel()
    {
        Model model = ModelLoader.Load(Application.streamingAssetsPath + "/MiniLMv6.sentis");

        Model modelWithMeanPooling = Functional.Compile(
            (input_ids, attention_mask, token_type_ids) =>
            {
                var tokenEmbeddings = model.Forward(input_ids, attention_mask, token_type_ids)[0];
                return MeanPooling(tokenEmbeddings, attention_mask);
            },
            (model.inputs[0], model.inputs[1], model.inputs[2])
        );

        return WorkerFactory.CreateWorker(backend, modelWithMeanPooling);
    }

    private void Update()
    {
        
        bool isPromptFieldVisible = promptField.gameObject.activeSelf;
        
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            promptField.gameObject.SetActive(!isPromptFieldVisible);
            _controller.enabled = !_controller.enabled;
            promptField.ActivateInputField();
        }
    }

    [SerializeField]
    private float similarity_threshold = 0.5f;
    public void CalculateSimilarity()
    {
        if (promptField.text.Length > 0)
        {
            int maxScoreIndex = -1;
            float maxScore = 0.0f;
            
            StartCoroutine(TranslateCoroutine(promptField.text, result =>
            {
                if (result != "Error")
                {
                    textSimilarity.text = "Prompt:" + promptField.text + " (" + result + ")\n";
                    
                    Debug.Log("Translated: " + result);
                    
                    for(int index=0; index<actionList.Count; index++)
                    {
                        var tokens1 = GetTokens(result);
                        var tokens2 = GetTokens(actionList[index]);

                        using TensorFloat embedding1 = GetEmbedding(tokens1);
                        using TensorFloat embedding2 = GetEmbedding(tokens2);

                        float score = GetDotScore(embedding1, embedding2);

                        if (maxScore < score)
                        {
                            maxScore = score;
                            maxScoreIndex = index;
                        }
                
                        textSimilarity.text += index.ToString() + ": " + actionList[index] + " " + score.ToString("F3") + "\n";
                        
                        if (maxScore > similarity_threshold)
                            DoAction(maxScoreIndex);                        
                    }
                }
                else
                {
                    Debug.Log("Translation error");
                }
            }));
        }
    }

    void UpdateCameraState(CameraState newCameraState, YoloState newYoloState = YoloState.Off)
    {
        activatedCamIndex = (int)newCameraState;
        foreach (KeyValuePair<int, GameObject> kvp in cameraDictionary)
        {
            if (kvp.Key == activatedCamIndex)
            {
                kvp.Value.SetActive(true);
            }
            else
            {
                kvp.Value.SetActive(false);
            }
        }
        if (newYoloState == YoloState.Off)
        {
            textYoloMode.SetActive(false);
            yoloView.SetActive(false);
            yoloMode.SetActive(false);
            return;
        }
        else
        {
            textYoloMode.SetActive(true);
            yoloView.SetActive(true);
            yoloMode.SetActive(true);
        }
    }

    void ToggleMood()
    {
        if (!_modernTheme)
        {
            _modernTheme = !_modernTheme;
            
            for (int i = 0; i<_interiorOjbects.Count; i++)
            {
                Renderer ren =_interiorOjbects[i].GetComponent<Renderer>();
                if (i==0)
                {
                    ren.material = _interiorMaterials[5];
                }
                else
                {
                    ren.material = _interiorMaterials[6];   
                }
            }
        }
        else
        {
            _modernTheme = !_modernTheme;
            for (int i =0; i<_interiorOjbects.Count; i++)
            {
                Renderer ren =_interiorOjbects[i].GetComponent<Renderer>();
                ren.material = _interiorMaterials[i];
            }
        }
    }
    void DoAction(int actionIndex)
    {
        Debug.Log("chosen action:" + actionIndex);
        switch (actionIndex)
        {
            case 0:
                UpdateCameraState(CameraState.ForkreitView, YoloState.On);
                break;
            case 1:
                UpdateCameraState(CameraState.NPCView);
                break;
            case 2:
                UpdateCameraState(CameraState.TopView);
                break;
            case 3:
                _reportController.StartCoroutine(_reportController.CheckAndMovePlayerTr(_player.transform));
                _reportController.StartCoroutine(_reportController.CheckAnimator());
                break;       
            case 4:
                if (playableDirector.state == PlayState.Paused)
                    playableDirector.Play();
                else if (playableDirector.state == PlayState.Playing)
                    playableDirector.Pause();
                break;
            case 5:
                ToggleMood();
                break;
        }
    }

    float GetDotScore(TensorFloat A, TensorFloat B)
    {
        var inputs = new Dictionary<string, Tensor>()
        {
            { "input_0", A },
            { "input_1", B }
        };
        dotScore.Execute(inputs);
        var output = dotScore.PeekOutput() as TensorFloat;
        output.CompleteOperationsAndDownload();
        return output[0];
    }

    TensorFloat GetEmbedding(List<int> tokens)
    {
        int N = tokens.Count;
        using var input_ids = new TensorInt(new TensorShape(1, N), tokens.ToArray());
        using var token_type_ids = new TensorInt(new TensorShape(1, N), new int[N]);
        int[] mask = new int[N];
        for (int i = 0; i < mask.Length; i++)
        {
            mask[i] = 1;
        }
        using var attention_mask = new TensorInt(new TensorShape(1, N), mask);

        var inputs = new Dictionary<string, Tensor>
        {
            {"input_0", input_ids },
            {"input_1", attention_mask },
            {"input_2", token_type_ids}
        };

        engine.Execute(inputs);

        var output = engine.TakeOutputOwnership("output_0") as TensorFloat;
        return output;
    }

    //Get average of token embeddings taking into account the attention mask
    FunctionalTensor MeanPooling(FunctionalTensor tokenEmbeddings, FunctionalTensor attentionMask)
    {
        var mask = attentionMask.Unsqueeze(-1).BroadcastTo(new[] { FEATURES });     //shape=(1,N,FEATURES)
        var A = FF.ReduceSum(tokenEmbeddings * mask, 1, false);                     //shape=(1,FEATURES)       
        var B = A / (FF.ReduceSum(mask, 1, false) + 1e-9f);                         //shape=(1,FEATURES)
        var C = FF.Sqrt(FF.ReduceSum(FF.Square(B), 1, true));                       //shape=(1,FEATURES)
        return B / C;                                                               //shape=(1,FEATURES)
    }

    IWorker CreateDotScoreModel()
    {
        Model dotScoreModel = Functional.Compile(
            (input1, input2) => Functional.ReduceSum(input1 * input2, 1),
            (InputDef.Float(new TensorShape(1, FEATURES)),
            InputDef.Float(new TensorShape(1, FEATURES)))
        );

        return WorkerFactory.CreateWorker(backend, dotScoreModel);
    }

    List<int> GetTokens(string text)
    {
        //split over whitespace
        string[] words = text.ToLower().Split(null);

        var ids = new List<int>
        {
            START_TOKEN
        };

        string s = "";

        foreach (var word in words)
        {
            int start = 0;
            for(int i = word.Length; i >= 0;i--)
            {
                string subword = start == 0 ? word.Substring(start, i) : "##" + word.Substring(start, i-start);
                int index = System.Array.IndexOf(tokens, subword);
                if (index >= 0)
                {
                    ids.Add(index);
                    s += subword + " ";
                    if (i == word.Length) break;
                    start = i;
                    i = word.Length + 1;
                }
            }
        }

        ids.Add(END_TOKEN);
        return ids;
    }

    private void OnDestroy()
    { 
        dotScore?.Dispose();
        engine?.Dispose();
    }

    private IEnumerator TranslateCoroutine(string word, System.Action<string> callback)
    {
        var toLanguage = "en";
        var fromLanguage = "ko";
        
        string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={fromLanguage}&tl={toLanguage}&dt=t&q={UnityWebRequest.EscapeURL(word)}";

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Translation request error: " + webRequest.error);
                callback("Error");
            }
            else
            {
                string response = webRequest.downloadHandler.text;
                try
                {
                    response = response.Substring(4, response.IndexOf("\"", 4, System.StringComparison.Ordinal) - 4);
                    callback(response);
                }
                catch
                {
                    callback("Error");
                }
            }
        }
    }
}
