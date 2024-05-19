using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class CustomSentisManager : MonoBehaviour
{
    public CustomModelWhisper whisperObject;
    public CustomModelMiniLM miniLMObject;
    public CustomModelYOLO yoloObject;
    public List<string> actionList;

    public TMP_Text textSimilarity;

    public TMP_InputField promptField;
    public float similarityThreshold = 0.5f;

    enum YoloState
    {
        Toggle,
        On,
        Off
    }
    
    void Start()
    {
        promptField.gameObject.SetActive(false);
    }

    void Update()
    {
        bool isPromptFieldVisible = promptField.gameObject.activeSelf;

        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (isPromptFieldVisible)
            {
                promptField.gameObject.SetActive(false);
            }
            else
            {
                promptField.gameObject.SetActive(true);
                promptField.ActivateInputField();
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

                    List<float> scoreList = new List<float>();
                    
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
                            textSimilarity.text += "<color=#FFA500>" + scoreList[index].ToString("F3") + " " + actionList[index] + "</color>\n";
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
    
    void DoAction(int actionIndex)
    {
        Debug.Log("chosen action:" + actionIndex);
        switch (actionIndex)
        {
            case 0:
                SetYoloMode(YoloState.Off);
                break;
            case 1:
                SetYoloMode(YoloState.Off);
                break;
            case 2:
                SetYoloMode(YoloState.Off);
                break;
            case 3:
                SetYoloMode(YoloState.Toggle);
                break;
            case 4:
                break;       
            case 5:
                break;
            case 6:
                break;
        }
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
