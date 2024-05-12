using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Sentis;
using UnityEngine;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class ModelWhisper : MonoBehaviour
{
    [SerializeField]
    private string logMelSpectroModelName = "LogMelSepctro.sentis";
    [SerializeField]
    private string encoderModelName = "AudioEncoder_Small.sentis";
    [SerializeField]
    private string decoderModelName = "AudioDecoder_Small.sentis";
    [SerializeField]
    private string vocabName = "vocab.json";
    
    private IWorker logMelSpectroEngine;
    private IWorker encoderEngine;
    private IWorker decoderEngine;
    
    const BackendType backend = BackendType.GPUCompute;
    
    //Audio
    private AudioClip audioClip;
    public bool isRecording = false;
    const int maxSamples = 30 * 16000;
    
    int numSamples;
    float[] data;
    
    //Tokens
    string[] tokens;
    int currentToken = 0;
    int[] outputTokens = new int[maxTokens];
    
    // Used for special character decoding
    int[] whiteSpaceCharacters = new int[256];

    TensorFloat encodedAudio;

    bool transcribe = false;
    string outputString = "";

    const int maxTokens = 100;
    const int END_OF_TEXT = 50257;
    const int START_OF_TRANSCRIPT = 50258;
    const int ENGLISH = 50259;
    const int GERMAN = 50261;
    const int KOREAN = 50264;
    const int FRENCH = 50265;
    const int TRANSCRIBE = 50359; //for speech-to-text in specified language
    const int TRANSLATE = 50358;  //for speech-to-text then translate to English
    const int NO_TIME_STAMPS = 50363; 
    const int START_TIME = 50364;
    
    void Start()
    {
        Model logMelSpectroModel = ModelLoader.Load(Application.streamingAssetsPath +"/"+ logMelSpectroModelName);
        Model encoderModel = ModelLoader.Load(Application.streamingAssetsPath +"/"+ encoderModelName);
        Model decoderModel = ModelLoader.Load(Application.streamingAssetsPath +"/"+ decoderModelName);
        Model decoderModelWithArgMax = Functional.Compile(
            (tokens, audio) => Functional.ArgMax(decoderModel.Forward(tokens, audio)[0], 2),
            (decoderModel.inputs[0], decoderModel.inputs[1])
        );
        
        logMelSpectroEngine = WorkerFactory.CreateWorker(backend, logMelSpectroModel);
        encoderEngine = WorkerFactory.CreateWorker(backend, encoderModel);
        decoderEngine = WorkerFactory.CreateWorker(backend, decoderModelWithArgMax);
        
        GetTokens();
        SetupWhiteSpaceShifts();
    }

    public void RunWhisper(Action<string> onWhisperCompleted)
    {
        StartCoroutine(WhiserCoroutine(onWhisperCompleted));
    }

    IEnumerator WhiserCoroutine(Action<string> onWhisperCompleted)
    {
        while (transcribe && currentToken < outputTokens.Length - 1)
        {
            using var tokensSoFar = new TensorInt(new TensorShape(1, outputTokens.Length), outputTokens);

            var inputs = new Dictionary<string, Tensor>
            {
                {"input_0", tokensSoFar },
                {"input_1", encodedAudio }
            };

            decoderEngine.Execute(inputs);
            var tokensPredictions = decoderEngine.PeekOutput() as TensorInt;
            tokensPredictions.CompleteOperationsAndDownload();

            int ID = tokensPredictions[currentToken];

            outputTokens[++currentToken] = ID;

            if (ID == END_OF_TEXT)
            {
                transcribe = false;
                outputString = GetUnicodeText(outputString);
                onWhisperCompleted?.Invoke(outputString);
                
                Debug.Log(outputString);
                yield break;
            }
            else if (ID >= tokens.Length)
            {
                outputString += $"(time={(ID - START_TIME) * 0.02f})";
            }
            else
            {
                outputString += tokens[ID];
            }
            
            yield return null;    
        }
    }
    
    public void StartRecording()
    {
        isRecording = true;
        
        if(audioClip != null)
            AudioClip.Destroy(audioClip);
        
        audioClip = Microphone.Start(null, false, 30, 16000);
        Debug.Log("Recording started.");
    }

    public void StopRecording()
    {
        isRecording = false;
        Microphone.End(null);

        if (audioClip != null)
        {
            SaveRecordedClip();
        }

        Debug.Log("Recording stopped.");
    }

    private void SaveRecordedClip()
    {
        AudioSource audioSource = GetComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.Play();
        
        LoadAudioClip();
    }
    
    void LoadAudioClip()
    {
        LoadAudio();
        EncodeAudio();
        transcribe = true;
        outputString = "";
        
        outputTokens[0] = START_OF_TRANSCRIPT;
        outputTokens[1] = KOREAN; //ENGLISH;// GERMAN;//FRENCH;//
        outputTokens[2] = TRANSCRIBE; //TRANSLATE;//TRANSCRIBE;
        outputTokens[3] = NO_TIME_STAMPS;// START_TIME;//
        currentToken = 3;
    }

    void LoadAudio()
    {
        if(audioClip.frequency != 16000)
        {
            Debug.Log($"The audio clip should have frequency 16kHz. It has frequency {audioClip.frequency / 1000f}kHz");
            return;
        }

        numSamples = audioClip.samples;

        if (numSamples > maxSamples)
        {
            Debug.Log($"The AudioClip is too long. It must be less than 30 seconds. This clip is {numSamples/ audioClip.frequency} seconds.");
            return;
        }

        data = new float[numSamples];
        audioClip.GetData(data, 0);
    }

    void GetTokens()
    {
        var jsonText = File.ReadAllText(Application.streamingAssetsPath + "/" + vocabName);
        var vocab = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>(jsonText);
        tokens = new string[vocab.Count];
        foreach(var item in vocab)
        {
            tokens[item.Value] = item.Key;
        }
    }

    void EncodeAudio()
    {
        using var input = new TensorFloat(new TensorShape(1, numSamples), data);

        logMelSpectroEngine.Execute(input);
        var spectroOutput = logMelSpectroEngine.PeekOutput() as TensorFloat;

        encoderEngine.Execute(spectroOutput);
        encodedAudio = encoderEngine.PeekOutput() as TensorFloat;
    }
    
    // Translates encoded special characters to Unicode
    string GetUnicodeText(string text)
    {
        var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(ShiftCharacterDown(text));
        return Encoding.UTF8.GetString(bytes);
    }

    string ShiftCharacterDown(string text)
    {
        string outText = "";

        foreach (char letter in text)
        {
            outText += ((int)letter <= 256) ? letter :
                (char)whiteSpaceCharacters[(int)(letter - 256)];
        }
        return outText;
    }

    void SetupWhiteSpaceShifts()
    {
        for (int i = 0, n = 0; i < 256; i++)
        {
            if (IsWhiteSpace((char)i)) whiteSpaceCharacters[n++] = i;
        }
    }

    bool IsWhiteSpace(char c)
    {
        return !((33 <= c && c <= 126) || (161 <= c && c <= 172) || (187 <= c && c <= 255));
    }

    private void OnDestroy()
    {
        logMelSpectroEngine?.Dispose();
        encoderEngine?.Dispose();
        decoderEngine?.Dispose();
    }
}
