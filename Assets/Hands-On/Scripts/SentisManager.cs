using System;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Playables;

public class SentisManager : MonoBehaviour
{
    public ModelWhisper whisperObject;
    public ModelMiniLM miniLMObject;
    public ModelYOLO yoloObject;
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
    
    private int _activatedCamIndex = 1;

    public List<GameObject> _interiorObjects = new List<GameObject>(); 
    public List<Material> _interiorMaterials= new List<Material>(); 
    private bool _modernTheme;

    private GameObject _player;
    private GameObject _reporter; 

    public TMP_Text textSimilarity;
    private string _dashLine = new string('=', 43);

    public PlayableDirector robotsDirector;

    public TMP_InputField promptField;
    private ThirdPersonController _controller;
    private WorkerAgent _reportController;
    
    public float similarityThreshold = 0.5f;

    void Start()
    {
        promptField.gameObject.SetActive(false);
        _player = GameObject.FindWithTag("Player");
        _reporter = GameObject.FindWithTag("Reporter");
        _controller = _player.GetComponent<ThirdPersonController>();
        _reportController =  _reporter.GetComponent<WorkerAgent>();
        robotsDirector.Pause();
    }

    void Update()
    {
        bool isPromptFieldVisible = promptField.gameObject.activeSelf;

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
        
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            whisperObject.StartRecording();
        }
        else if (Input.GetKeyUp(KeyCode.LeftControl))
        {
            bool success = whisperObject.StopRecording();
            if (success)
            {
                whisperObject.RunWhisper(result =>
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

            textSimilarity.text = "<color=#FF5733><b>Prompt: " + promptField.text + "</b></color>\n";

            StartCoroutine(TranslateCoroutine(promptField.text, result =>
            {
                if (result != "Error")
                {
                    textSimilarity.text += "<color=#FF5733><b>" + "Translated: " + result + "</b></color>\n";
                    textSimilarity.text += _dashLine + "</color=FFFFF>\n";
                    //Debug.Log("Translated: " + result);
                    
                    List<float> scoreList = new List<float>();
                    
                    //
                    for(int index=0; index<actionList.Count; index++)
                    {
                        score = miniLMObject.RunMiniLM(result, actionList[index]);

                        if (maxScore < score)
                        {
                            maxScore = score;
                            maxScoreIndex = index;
                        }
                        
                        scoreList.Add(score);
                    }

                    for (int index = 0; index < scoreList.Count; index++)
                    {
                        if (index == maxScoreIndex && scoreList[index] >= similarityThreshold)
                        {
                            textSimilarity.text += "<color=#FF5733>" + scoreList[index].ToString("F3") + " " + "</color><color=#FFA500>" + actionList[index] + "</color>\n";
                        }
                        else
                        {
                            textSimilarity.text += scoreList[index].ToString("F3") + " " + actionList[index] + "\n";
                        }
                    }

                    if (maxScore >= similarityThreshold)
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
        _activatedCamIndex = (int)newCameraState;
        for (int i=0; i < Enum.GetValues(typeof(CameraState)).Length; i++ )
        {
            if (i == _activatedCamIndex)
            {
                Cameras[i].SetActive(true);
            }
            else
            {
                Cameras[i].SetActive(false);
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
        if (robotsDirector.state == PlayState.Paused)
            robotsDirector.Play();
        else if (robotsDirector.state == PlayState.Playing)
            robotsDirector.Pause();
    }

    void SetYoloMode(YoloState yoloState)
    {
        switch (yoloState)
        {
            case YoloState.On:
                yoloObject.StartYolo();
            break;
            case YoloState.Off:
                yoloObject.StopYolo();
            break;
            case YoloState.Toggle:
                yoloObject.ToggleYolo();
            break;
        }
        CheckCullingMask(yoloState);
    }

    void CheckCullingMask(YoloState yoloState)
    {
        GameObject gameObject = Cameras[_activatedCamIndex];
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
        //Debug.Log("chosen action:" + actionIndex);
        switch (actionIndex)
        {
            case 0:
                UpdateCameraState(CameraState.ForkreitView);
                SetYoloMode(YoloState.Off);
                break;
            case 1:
                UpdateCameraState(CameraState.NPCView);
                SetYoloMode(YoloState.Off);
                break;
            case 2:
                UpdateCameraState(CameraState.TopView);
                SetYoloMode(YoloState.Off);
                break;
            case 3:
                SetYoloMode(YoloState.Toggle);
                break;
            case 4:
                UpdateCameraState(CameraState.NPCView);
                SetYoloMode(YoloState.Off);
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

        switch (whisperObject.speakerLanguage)
        {
            case ModelWhisper.WhisperLanguage.ENGLISH:
                fromLanguage = "en";
                break;
            case ModelWhisper.WhisperLanguage.KOREAN:
                fromLanguage = "ko";
                break;
            case ModelWhisper.WhisperLanguage.JAPAN:
                fromLanguage = "ja";
                break;
        }
        
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
