using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Playables;
using UnityEngine.Rendering.HighDefinition;

public class SentisManager : MonoBehaviour
{
    ModelWhisper _whisperObject;
    ModelMiniLM _miniLMObject;
    ModelYOLO _yoloObject;
    CustomPassVolume _yoloPass;
    UIManager _UIManager;
    public List<string> actionList;
    public List<GameObject> Cameras = new List<GameObject>();
    enum CameraState
    {
        ForkreitView,
        NPCView,
        TopView
    }
    
    Dictionary<int, GameObject> cameraDictionary = new Dictionary<int, GameObject>();
    int activatedCamIndex = 1;

    public List<GameObject> _interiorObjects = new List<GameObject>(); 
    public List<Material> _interiorMaterials= new List<Material>(); 
    bool _modernTheme;    

    GameObject _player;
    public GameObject _reporter; 

    public TMP_Text textSimilarity;
    public PlayableDirector _robotsDirector;

    [SerializeField]
    private TMP_InputField promptField;
    ThirdPersonController _controller;
    WorkerAgent _reportController;
    
    [SerializeField]
    private float similarity_threshold = 0.5f;

    void Start()
    {
        //Application.targetFrameRate= 60;
        promptField.gameObject.SetActive(false);
        _player = GameObject.FindWithTag("Player");
        _reporter = GameObject.FindWithTag("Reporter");
        _controller = _player.GetComponent<ThirdPersonController>();
        _reportController =  _reporter.GetComponent<WorkerAgent>();
        _whisperObject = GameObject.FindWithTag("Whisper").GetComponent<ModelWhisper>();
        _miniLMObject = GameObject.FindWithTag("MiniLM").GetComponent<ModelMiniLM>();
        _yoloObject = GameObject.FindWithTag("Yolo").GetComponent<ModelYOLO>();
        _yoloPass =  GameObject.FindWithTag("Yolo").GetComponent<CustomPassVolume>();
        _UIManager = GameObject.FindWithTag("UIManager").GetComponent<UIManager>();
        _robotsDirector.Pause();
        for (int i = 0; i < Cameras.Count; i++)
        {
            cameraDictionary.Add(i, Cameras[i]);
        }
    }

    void Update()
    {
        bool isPromptFieldVisible = promptField.gameObject.activeSelf;
        
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            promptField.gameObject.SetActive(!isPromptFieldVisible);
            _controller.enabled = !_controller.enabled;
            promptField.ActivateInputField();
        }
        
        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            if (_whisperObject.isRecording == false)
            {
                _whisperObject.StartRecording();
            }
        }
        else if (Input.GetKeyUp(KeyCode.RightBracket))
        {
            if (_whisperObject.isRecording == true)
            {
                _whisperObject.StopRecording();
                _whisperObject.RunWhisper(result =>
                {
                    Debug.Log("Result:" + result);
                    promptField.text = result;
                    promptField.onEndEdit.Invoke(promptField.text);
                });
            }
        }
    }
    
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
                    textSimilarity.text = promptField.text + " (" + result + ")\n";
                    
                    Debug.Log("Translated: " + result);
                    
                    for(int index=0; index<actionList.Count; index++)
                    {
                        float score = _miniLMObject.RunMiniLM(result, actionList[index]);

                        if (maxScore < score)
                        {
                            maxScore = score;
                            maxScoreIndex = index;
                        }
                
                        textSimilarity.text += index.ToString() + ": " + actionList[index] + " " + score.ToString("F3") + "\n";
                    }
                    
                    if (maxScore > similarity_threshold)
                        DoAction(maxScoreIndex);
                }
                else
                {
                    Debug.Log("Translation error");
                }
            }));
        }
    }
    
    void UpdateCameraState(CameraState newCameraState)
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
    }

    void ToggleMood()
    {
        if (!_modernTheme)
        {
            _modernTheme = !_modernTheme;
            
            for (int i = 0; i<_interiorObjects.Count; i++)
            {
                Renderer ren =_interiorObjects[i].GetComponent<Renderer>();
                ren.material = _interiorMaterials[4];
            }
        }
        else
        {
            _modernTheme = !_modernTheme;
            for (int i =0; i<_interiorObjects.Count; i++)
            {
                Renderer ren =_interiorObjects[i].GetComponent<Renderer>();
                ren.material = _interiorMaterials[i];
            }
        }
    }

    void RunPalletrobot()
    {
        if (_robotsDirector.state == PlayState.Paused)
            _robotsDirector.Play();
        else if (_robotsDirector.state == PlayState.Playing)
            _robotsDirector.Pause();
    }

    void EnableYolo()
    {
        _yoloPass.enabled = !_yoloPass.enabled;
        CheckCullingMask();
        _UIManager.AnimatedSprite(_UIManager._sentisAnimation);
    }

    void CheckCullingMask()
    {
        cameraDictionary.TryGetValue(activatedCamIndex, out GameObject gameObject);
        int playerLayerBit = 1 << LayerMask.NameToLayer("Player");
        Camera _cam = gameObject.GetComponent<Camera>();
        int currentMask = _cam.cullingMask;
        bool isPlayerLayerIncluded = (currentMask & playerLayerBit) != 0;

        if (isPlayerLayerIncluded)
        {
            currentMask &= ~playerLayerBit; 
            _cam.cullingMask = currentMask;
        }
        else
        {
            currentMask |= playerLayerBit;
            _cam.cullingMask = currentMask;
        }
    }
    
    void DoAction(int actionIndex)
    {
        Debug.Log("chosen action:" + actionIndex);
        switch (actionIndex)
        {
            case 0:
                UpdateCameraState(CameraState.ForkreitView);
                break;
            case 1:
                UpdateCameraState(CameraState.NPCView);
                break;
            case 2:
                UpdateCameraState(CameraState.TopView);
                break;
            case 3:
                EnableYolo();
                break;
            case 4:
                _reportController.StartCoroutine(_reportController.CheckAndMovePlayerTr(_player.transform));
                _reportController.StartCoroutine(_reportController.CheckAnimator());
                break;       
            case 5:
                RunPalletrobot();
                break;
            case 6:
                ToggleMood();
                break;
        }
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
