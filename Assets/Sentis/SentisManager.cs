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
        //if(isPromptFieldVisible == false && _controller.enabled == false)
        //    _controller.enabled = true;

        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (isPromptFieldVisible)
            {
                promptField.gameObject.SetActive(false);
                _controller.enabled = true;
            }
            else
            {
                promptField.gameObject.SetActive(true);
                promptField.ActivateInputField();
                _controller.enabled = false;
            }
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
                
                        textSimilarity.text += score.ToString("F3") + " " + actionList[index] + "\n";
                    }
                    
                    if (maxScore > similarity_threshold)
                        DoAction(maxScoreIndex);
                }
                else
                {
                    Debug.Log("Translation error");
                }

                promptField.text = "";
                promptField.gameObject.SetActive(false);
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

    void ToggleYolo()
    {
        _yoloObject.ToggleYolo();
        CheckCullingMask();
    }

    void CheckCullingMask()
    {
        if (Camera.main.name == "PlayerCamera")
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
    }
    
    void DoAction(int actionIndex)
    {
        Debug.Log("chosen action:" + actionIndex);
        switch (actionIndex)
        {
            case 0:
                _yoloObject.StopYolo();
                UpdateCameraState(CameraState.ForkreitView);
                break;
            case 1:
                _yoloObject.StopYolo();
                UpdateCameraState(CameraState.NPCView);
                break;
            case 2:
                _yoloObject.StopYolo();
                UpdateCameraState(CameraState.TopView);
                break;
            case 3:
                ToggleYolo();
                break;
            case 4:
                _yoloObject.StopYolo();
                UpdateCameraState(CameraState.NPCView);
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
