using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Sentis;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Text;
using TMPro;

/*
 *              Whisper Inference Code
 *              ======================
 *  
 *  Put this script on the Main Camera
 *  
 *  In Assets/StreamingAssets put:
 *  
 *  AudioDecoder_Tiny.sentis
 *  AudioEncoder_Tiny.sentis
 *  LogMelSepctro.sentis
 *  vocab.json
 * 
 *  Drag a 30s 16khz mono uncompressed audioclip into the audioClip field. 
 * 
 *  Install package com.unity.nuget.newtonsoft-json from packagemanger
 *  Install package com.unity.sentis
 * 
 */


public class RunWhisper : MonoBehaviour
{
    IWorker decoderEngine, encoderEngine, spectroEngine;

    const BackendType backend = BackendType.GPUCompute;

    // Link your audioclip here. Format must be 16Hz mono non-compressed.
    private AudioClip audioClip;

    public TMP_InputField promptField;

    // This is how many tokens you want. It can be adjusted.
    const int maxTokens = 100;

    //Special tokens see added tokens file for details
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
     
    int numSamples;
    float[] data;
    string[] tokens;

    int currentToken = 0;
    int[] outputTokens = new int[maxTokens];

    // Used for special character decoding
    int[] whiteSpaceCharacters = new int[256];

    TensorFloat encodedAudio;

    bool transcribe = false;
    string outputString = "";

    // Maximum size of audioClip (30s at 16kHz)
    const int maxSamples = 30 * 16000;

    [SerializeField] private string whisper_decoder_filename = "AudioDecoder_Small.sentis";
    [SerializeField] private string whisper_encoder_filename = "AudioEncoder_Small.sentis";

    void Start()
    {
        SetupWhiteSpaceShifts();

        GetTokens();

        Model decoder = ModelLoader.Load(Application.streamingAssetsPath + "/"+ whisper_decoder_filename);

        Model decoderWithArgMax = Functional.Compile(
            (tokens, audio) => Functional.ArgMax(decoder.Forward(tokens, audio)[0], 2),
            (decoder.inputs[0], decoder.inputs[1])
        );

        Model encoder = ModelLoader.Load(Application.streamingAssetsPath + "/" + whisper_encoder_filename);
        Model spectro = ModelLoader.Load(Application.streamingAssetsPath + "/LogMelSepctro.sentis");

        decoderEngine = WorkerFactory.CreateWorker(backend, decoderWithArgMax);
        encoderEngine = WorkerFactory.CreateWorker(backend, encoder);
        spectroEngine = WorkerFactory.CreateWorker(backend, spectro);
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
    
    //private AudioClip recordedClip;
    private bool isRecording = false;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            if (isRecording == false)
            {
                StartRecording();
            }
        }
        else if (Input.GetKeyUp(KeyCode.RightBracket))
        {
            if (isRecording == true)
            {
                StopRecording();
            }
        }
        
        ExecuteWhisperInference();
    }
    
    private void StartRecording()
    {
        isRecording = true;
        
        if(audioClip != null)
            AudioClip.Destroy(audioClip);
        
        audioClip = Microphone.Start(null, false, 30, 16000);
        Debug.Log("Recording started.");
    }

    private void StopRecording()
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
        audioSource.clip = null;
        audioSource.clip = audioClip;
        audioSource.Play();
        
        LoadAudioClip();
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
        var jsonText = File.ReadAllText(Application.streamingAssetsPath + "/vocab.json");
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

        spectroEngine.Execute(input);
        var spectroOutput = spectroEngine.PeekOutput() as TensorFloat;

        encoderEngine.Execute(spectroOutput);
        encodedAudio = encoderEngine.PeekOutput() as TensorFloat;
    }

    void ExecuteWhisperInference()
    {
        if (transcribe && currentToken < outputTokens.Length - 1)
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
                Debug.Log(outputString);
                promptField.text = outputString;
                promptField.onEndEdit.Invoke(promptField.text);
            }
            else if (ID >= tokens.Length)
            {
                outputString += $"(time={(ID - START_TIME) * 0.02f})";
            }
            else outputString += tokens[ID];
        }
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
        //return !(('!' <= c && c <= '~') || ('�' <= c && c <= '�') || ('�' <= c && c <= '�'));
        return !((33 <= c && c <= 126) || (161 <= c && c <= 172) || (187 <= c && c <= 255));
    }

    private void OnApplicationQuit()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
    }

    private void OnDestroy()
    {
        decoderEngine?.Dispose();
        encoderEngine?.Dispose();
        spectroEngine?.Dispose();
    }
}
