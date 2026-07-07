using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Diagnostics;

namespace ParkinsonAppWindows.Services
{
    public class OnnxService
    {
        public double RunInference(string modelPath, float[] features)
        {
            try
            {
                var options = new SessionOptions();
                using var session = new InferenceSession(modelPath, options);

                
                var inputName = session.InputMetadata.Keys.First();

                var inputTensor = new DenseTensor<float>(features, new[] { 1, features.Length });
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };

                using var results = session.Run(inputs);

      

                var output = results.FirstOrDefault(x => x.Name == "output_probability" || x.Name == "probabilities")
                             ?? results.ElementAtOrDefault(1); 

                if (output == null) return 0.0;

                
                if (output.Value is DenseTensor<float> tensor)
                {
                    var arr = tensor.ToArray();

                    if (arr.Length >= 2)
                    {
                        return arr[1] * 100.0;
                    }
                    else if (arr.Length == 1)
                    {
                        return arr[0] * 100.0;
                    }
                }

                if (output.Value is IEnumerable<IDictionary<long, float>> mapLong)
                {
                    var dict = mapLong.FirstOrDefault();
                    if (dict != null && dict.TryGetValue(1L, out float p)) return p * 100.0;
                }

                return 0.0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in model {System.IO.Path.GetFileName(modelPath)}: {ex.Message}");
                return 0.0;
            }
        }
    }
}