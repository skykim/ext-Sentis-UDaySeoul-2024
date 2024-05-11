using System.Collections.Generic;
using System.IO;
using Unity.Sentis;
using FF = Unity.Sentis.Functional;
using UnityEngine;

public class ModelMiniLM : MonoBehaviour
{
    [SerializeField] private string modelName = "MiniLMv6.sentis";
    [SerializeField] private string vocabName = "vocab.txt";

    private IWorker engine, dotScore;
    const BackendType backend = BackendType.GPUCompute;

    //Token
    const int START_TOKEN = 101;
    const int END_TOKEN = 102;

    //Store the vocabulary
    string[] tokens;
    const int FEATURES = 384; //size of feature space

    void Start()
    {
        Model model = ModelLoader.Load(Application.streamingAssetsPath + "/" + modelName);
        Model modelWithMeanPooling = Functional.Compile(
            (input_ids, attention_mask, token_type_ids) =>
            {
                var tokenEmbeddings = model.Forward(input_ids, attention_mask, token_type_ids)[0];
                return MeanPooling(tokenEmbeddings, attention_mask);
            },
            (model.inputs[0], model.inputs[1], model.inputs[2])
        );

        Model dotScoreModel = Functional.Compile(
            (input1, input2) => Functional.ReduceSum(input1 * input2, 1),
            (InputDef.Float(new TensorShape(1, FEATURES)),
                InputDef.Float(new TensorShape(1, FEATURES)))
        );

        engine = WorkerFactory.CreateWorker(backend, modelWithMeanPooling);
        dotScore = WorkerFactory.CreateWorker(backend, dotScoreModel);

        tokens = File.ReadAllLines(Application.streamingAssetsPath + "/" + vocabName);
    }

    FunctionalTensor MeanPooling(FunctionalTensor tokenEmbeddings, FunctionalTensor attentionMask)
    {
        var mask = attentionMask.Unsqueeze(-1).BroadcastTo(new[] { FEATURES }); //shape=(1,N,FEATURES)
        var A = FF.ReduceSum(tokenEmbeddings * mask, 1, false); //shape=(1,FEATURES)       
        var B = A / (FF.ReduceSum(mask, 1, false) + 1e-9f); //shape=(1,FEATURES)
        var C = FF.Sqrt(FF.ReduceSum(FF.Square(B), 1, true)); //shape=(1,FEATURES)
        return B / C; //shape=(1,FEATURES)
    }

    public float RunMiniLM(string sentence1, string sentence2)
    {
        var tokens1 = GetTokens(sentence1);
        var tokens2 = GetTokens(sentence2);

        using TensorFloat embedding1 = GetEmbedding(tokens1);
        using TensorFloat embedding2 = GetEmbedding(tokens2);

        float score = GetDotScore(embedding1, embedding2);
        return score;
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
            { "input_0", input_ids },
            { "input_1", attention_mask },
            { "input_2", token_type_ids }
        };

        engine.Execute(inputs);

        var output = engine.TakeOutputOwnership("output_0") as TensorFloat;
        return output;
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
            for (int i = word.Length; i >= 0; i--)
            {
                string subword = start == 0 ? word.Substring(start, i) : "##" + word.Substring(start, i - start);
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
        engine?.Dispose();
        dotScore?.Dispose();
    }
}