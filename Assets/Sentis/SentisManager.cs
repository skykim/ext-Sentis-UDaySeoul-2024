using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Playables;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

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
    enum YoloState
    {
        Toggle,
        On,
        Off
    }
    
    int activatedCamIndex = 1;

    public List<GameObject> _interiorObjects = new List<GameObject>(); 
    public List<Material> _interiorMaterials= new List<Material>(); 
    bool _modernTheme;    

    GameObject _player;
    public GameObject _reporter; 

    public TMP_Text textSimilarity;
    string _dashLine = new string('=', 34);

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
                    promptField.text = "";
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
            float score;
            int _Dindex = 0;

            textSimilarity.text = "<color=#FF5733><b>KOR (Recorded) : " + promptField.text + "</b></color>\n";

            StartCoroutine(TranslateCoroutine(promptField.text, result =>
            {
                if (result != "Error")
                {
                    textSimilarity.text += "<color=#FF5733><b>" + "ENG (Translated) : " + promptField.text + " " + result + "</b></color>\n";
                    textSimilarity.text += _dashLine + "</color=FFFFF>\n";
                    Debug.Log("Translated: " + result);
                    Dictionary<float, string> textes = new Dictionary<float, string>();
                    
                    for(int index=0; index<actionList.Count; index++)
                    {
                        score = _miniLMObject.RunMiniLM(result, actionList[index]);

                        if (maxScore < score)
                        {
                            maxScore = score;
                            maxScoreIndex = index;
                        }
                        
                        textes.Add(score, actionList[index]);
                    }
                    
                    foreach (KeyValuePair<float, string> pair in textes)
                    {
                        if (_Dindex == maxScoreIndex)
                        {
                            textSimilarity.text += "<color=#FF5733><b>" + pair.Key.ToString("F3") + "</b></color> <color=#FFA500>" + actionList[_Dindex] + "</color>\n";
                        }
                        else
                        {
                            textSimilarity.text += pair.Key.ToString("F3") + " " + actionList[_Dindex] + "</color>\n";
                        }
                        _Dindex++;
                    }

                    if (maxScore >= similarity_threshold)
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
    
    void UpdateCameraState(CameraState newCameraState, YoloState YoloState)
    {
        activatedCamIndex = (int)newCameraState;
        for (int i=0; i < Enum.GetValues(typeof(CameraState)).Length; i++ )
        {
            if (i == activatedCamIndex)
            {
                Cameras[i].SetActive(true);
            }
            else
            {
                Cameras[i].SetActive(false);
            }

        }
        YoloMode(YoloState);
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

    void YoloMode(YoloState yoloState)
    {
        switch (yoloState)
        {
            case YoloState.On:
                _yoloObject.StartYolo();
            break;
            case YoloState.Off:
                _yoloObject.StopYolo();
            break;
            case YoloState.Toggle:
                _yoloObject.ToggleYolo();
            break;
        }
        CheckCullingMask(yoloState);
        //_UIManager.AnimatedSprite(_UIManager._sentisAnimation);
    }

    void CheckCullingMask(YoloState yoloState)
    {
        GameObject gameObject = Cameras[activatedCamIndex];
        int playerLayerBit = 1 << LayerMask.NameToLayer("Player");
        Camera _cam = gameObject.GetComponent<Camera>();
        int currentMask = _cam.cullingMask;
        bool isPlayerLayerIncluded = (currentMask & playerLayerBit) != 0;

        if (yoloState == YoloState.Off)
        {
            if (!isPlayerLayerIncluded)
            {
                currentMask |= playerLayerBit;
                _cam.cullingMask = currentMask;
            }
        }

        if (yoloState == YoloState.Toggle)
        {
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
                UpdateCameraState(CameraState.ForkreitView, YoloState.Off);
                break;
            case 1:
                UpdateCameraState(CameraState.NPCView, YoloState.Off);
                break;
            case 2:
                UpdateCameraState(CameraState.TopView, YoloState.Off);
                break;
            case 3:
                YoloMode(YoloState.Toggle);
                break;
            case 4:
                UpdateCameraState(CameraState.NPCView, YoloState.Off);
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
