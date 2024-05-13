using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Sentis;
using UnityEngine;
using System.IO;

public class ModelWhisper : MonoBehaviour
{
    public string logMelSpectroModelName = "LogMelSepctro.sentis";
    public string encoderModelName = "AudioEncoder_Small.sentis";
    public string decoderModelName = "AudioDecoder_Small.sentis";
    public string vocabName = "vocab.json";
    
    private IWorker _logMelSpectroEngine;
    private IWorker _encoderEngine;
    private IWorker _decoderEngine;
    
    const BackendType BACKEND = BackendType.GPUCompute;
    
    //Audio
    private AudioClip _audioClip;
    private const int MAX_RECORDING_TIME = 30;
    private const int AUDIO_SAMPLING_RATE = 16000;
    private const int maxSamples = MAX_RECORDING_TIME * AUDIO_SAMPLING_RATE;
    private int _numSamples;
    private float[] _data;
    
    //Tokens
    private string[] _tokens;
    private int _currentToken = 0;
    private int[] _outputTokens = new int[maxTokens];
    
    // Used for special character decoding
    private int[] _whiteSpaceCharacters = new int[256];

    private TensorFloat _encodedAudio;

    private bool _transcribe = false;
    private string _outputString = "";

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
        
        _logMelSpectroEngine = WorkerFactory.CreateWorker(BACKEND, logMelSpectroModel);
        _encoderEngine = WorkerFactory.CreateWorker(BACKEND, encoderModel);
        _decoderEngine = WorkerFactory.CreateWorker(BACKEND, decoderModelWithArgMax);
        
        GetTokens();
        SetupWhiteSpaceShifts();
    }

    public void RunWhisper(Action<string> onWhisperCompleted)
    {
        StartCoroutine(WhiserCoroutine(onWhisperCompleted));
    }

    IEnumerator WhiserCoroutine(Action<string> onWhisperCompleted)
    {
        while (_transcribe && _currentToken < _outputTokens.Length - 1)
        {
            using var tokensSoFar = new TensorInt(new TensorShape(1, _outputTokens.Length), _outputTokens);

            var inputs = new Dictionary<string, Tensor>
            {
                {"input_0", tokensSoFar },
                {"input_1", _encodedAudio }
            };

            _decoderEngine.Execute(inputs);
            var tokensPredictions = _decoderEngine.PeekOutput() as TensorInt;
            tokensPredictions.CompleteOperationsAndDownload();

            int ID = tokensPredictions[_currentToken];

            _outputTokens[++_currentToken] = ID;

            if (ID == END_OF_TEXT)
            {
                _transcribe = false;
                _outputString = GetUnicodeText(_outputString);
                onWhisperCompleted?.Invoke(_outputString);
                
                //Debug.Log(_outputString);
                yield break;
            }
            else if (ID >= _tokens.Length)
            {
                _outputString += $"(time={(ID - START_TIME) * 0.02f})";
            }
            else
            {
                _outputString += _tokens[ID];
            }
            
            yield return null;    
        }
    }
    
    public void StartRecording()
    {
        if(_audioClip != null)
        {
            AudioClip.Destroy(_audioClip);
            _audioClip = null;
        }
        
        _audioClip = Microphone.Start(null, false, MAX_RECORDING_TIME, AUDIO_SAMPLING_RATE);
        Debug.Log("Recording started.");
    }

    public bool StopRecording()
    {
        Microphone.End(null);

        if (_audioClip != null)
        {
            SaveRecordedClip();
        }
        else
        {
            Debug.LogWarning("No audio clip recorded.");
            return false;
        }

        Debug.Log("Recording stopped.");
        return true;
    }

    private void SaveRecordedClip()
    {
        AudioSource audioSource = GetComponent<AudioSource>();
        audioSource.clip = _audioClip;
        audioSource.Play();
        
        LoadAudioClip();
        
        // Destroy the audio clip after loading to free up memory
        AudioClip.Destroy(_audioClip);
        _audioClip = null;
    }
    
    void LoadAudioClip()
    {
        LoadAudio();
        EncodeAudio();
        _transcribe = true;
        _outputString = "";
        
        _outputTokens[0] = START_OF_TRANSCRIPT;
        _outputTokens[1] = KOREAN; //ENGLISH;// GERMAN;//FRENCH;//
        _outputTokens[2] = TRANSCRIBE; //TRANSLATE;//TRANSCRIBE;
        _outputTokens[3] = NO_TIME_STAMPS;// START_TIME;//
        _currentToken = 3;
    }

    void LoadAudio()
    {
        if(_audioClip.frequency != AUDIO_SAMPLING_RATE)
        {
            Debug.Log($"The audio clip should have frequency 16kHz. It has frequency {_audioClip.frequency / 1000f}kHz");
            return;
        }

        _numSamples = _audioClip.samples;

        if (_numSamples > maxSamples)
        {
            Debug.Log($"The AudioClip is too long. It must be less than 30 seconds. This clip is {_numSamples/ _audioClip.frequency} seconds.");
            return;
        }

        _data = new float[_numSamples];
        _audioClip.GetData(_data, 0);
    }

    void GetTokens()
    {
        var jsonText = File.ReadAllText(Application.streamingAssetsPath + "/" + vocabName);
        var vocab = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>(jsonText);
        _tokens = new string[vocab.Count];
        foreach(var item in vocab)
        {
            _tokens[item.Value] = item.Key;
        }
    }

    void EncodeAudio()
    {
        using var input = new TensorFloat(new TensorShape(1, _numSamples), _data);

        _logMelSpectroEngine.Execute(input);
        var spectroOutput = _logMelSpectroEngine.PeekOutput() as TensorFloat;

        _encoderEngine.Execute(spectroOutput);
        _encodedAudio = _encoderEngine.PeekOutput() as TensorFloat;
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
                (char)_whiteSpaceCharacters[(int)(letter - 256)];
        }
        return outText;
    }

    void SetupWhiteSpaceShifts()
    {
        for (int i = 0, n = 0; i < 256; i++)
        {
            if (IsWhiteSpace((char)i)) _whiteSpaceCharacters[n++] = i;
        }
    }

    bool IsWhiteSpace(char c)
    {
        return !((33 <= c && c <= 126) || (161 <= c && c <= 172) || (187 <= c && c <= 255));
    }

    private void OnDestroy()
    {
        _logMelSpectroEngine?.Dispose();
        _encoderEngine?.Dispose();
        _decoderEngine?.Dispose();
    }
}
