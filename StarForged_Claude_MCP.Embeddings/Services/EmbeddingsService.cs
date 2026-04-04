using Microsoft.Extensions.Configuration;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using StarForged_Claude_MCP.Embeddings.Services.Models;

namespace StarForged_Claude_MCP.Embeddings.Services;

internal sealed class EmbeddingsService : IDisposable
{
    private readonly VectorCacheService _vectorCache;
    private readonly InferenceSession _session;

    public EmbeddingsService(VectorCacheService vectorCache, IConfiguration configuration)
    {
        _vectorCache = vectorCache;

        var modelPath = Path.Combine(AppContext.BaseDirectory, "model.onnx");

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"ONNX model not found at: {modelPath}");
        }

        _session = new InferenceSession(modelPath);
    }

    public float[] GenerateEmbeddings(Chunk input)
    {
        var tokens = input.Tokens;

        var inputIds = new DenseTensor<long>(new[] { 1, tokens.Length });
        var attentionMask = new DenseTensor<long>(new[] { 1, tokens.Length });
        var tokenTypeIds = new DenseTensor<long>(new[] { 1, tokens.Length });

        for (int i = 0; i < tokens.Length; i++)
        {
            inputIds[0, i] = tokens[i].InputIds;
            attentionMask[0, i] = tokens[i].AttentionMask;
            tokenTypeIds[0, i] = tokens[i].TokenTypeIds;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds)
        };

        using var results = _session.Run(inputs);
        using var firstResult = results.First();
        var outputTensor = firstResult.AsTensor<float>();

        // last_hidden_state shape: [1, seq_len, hidden_size]
        int seqLen = outputTensor.Dimensions[1];
        int hiddenSize = outputTensor.Dimensions[2];

        // Mean pool over non-padding tokens using the attention mask
        var embedding = new float[hiddenSize];
        int count = 0;

        for (int t = 0; t < seqLen; t++)
        {
            if (attentionMask[0, t] == 1)
            {
                for (int h = 0; h < hiddenSize; h++)
                {
                    embedding[h] += outputTensor[0, t, h];
                }
                count++;
            }
        }

        for (int h = 0; h < hiddenSize; h++)
        {
            embedding[h] /= count;
        }

        return embedding;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
