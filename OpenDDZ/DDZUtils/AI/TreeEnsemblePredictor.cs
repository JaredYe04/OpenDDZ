using OpenDDZ.DDZUtils.AI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenDDZ.DDZUtils.AI
{
    public class TreeEnsembleModel
    {
        public int Version { get; set; }
        public string[] FeatureNames { get; set; }
        public float[] ScalerMean { get; set; }
        public float[] ScalerStd { get; set; }
        public List<FlatTree> Trees { get; set; } = new List<FlatTree>();
        public bool IsValid { get; set; }
    }

    public class FlatTree
    {
        public int Root { get; set; }
        public List<TreeNode> Nodes { get; set; } = new List<TreeNode>();
    }

    public class TreeNode
    {
        public int Feature { get; set; } = -1;
        public float Threshold { get; set; }
        public int Left { get; set; } = -1;
        public int Right { get; set; } = -1;
        public float Value { get; set; }
        public bool IsLeaf => Feature < 0;
    }

    public class TreeEnsemblePredictor
    {
        private readonly TreeEnsembleModel _model;

        public TreeEnsemblePredictor(TreeEnsembleModel model)
        {
            _model = model;
        }

        public static TreeEnsemblePredictor Load(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                if (path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    var binModel = BinaryModelFormat.Read(path);
                    return binModel != null && binModel.IsValid
                        ? new TreeEnsemblePredictor(binModel)
                        : null;
                }

                var json = JObject.Parse(File.ReadAllText(path));
                int version = json["version"]?.Value<int>() ?? 1;
                if (version < 2) return null;

                var model = new TreeEnsembleModel
                {
                    Version = version,
                    FeatureNames = json["feature_names"]?.ToObject<string[]>() ?? BotFeatureExtractor.FeatureNames,
                    ScalerMean = json["scaler"]?["mean"]?.ToObject<float[]>() ?? new float[0],
                    ScalerStd = json["scaler"]?["std"]?.ToObject<float[]>() ?? new float[0]
                };

                foreach (var t in json["trees"] ?? new JArray())
                {
                    if (model.Trees.Count >= 60) break;
                    var tree = new FlatTree { Root = t["root"]?.Value<int>() ?? 0 };
                    foreach (var n in t["nodes"] ?? new JArray())
                    {
                        tree.Nodes.Add(new TreeNode
                        {
                            Feature = n["feature"]?.Value<int>() ?? -1,
                            Threshold = n["threshold"]?.Value<float>() ?? 0f,
                            Left = n["left"]?.Value<int>() ?? -1,
                            Right = n["right"]?.Value<int>() ?? -1,
                            Value = n["value"]?.Value<float>() ?? 0f
                        });
                    }
                    model.Trees.Add(tree);
                }

                model.IsValid = model.Trees.Count > 0 &&
                    model.ScalerMean.Length == BotFeatureExtractor.FeatureDim &&
                    model.ScalerStd.Length == BotFeatureExtractor.FeatureDim &&
                    model.Trees.All(t => t.Nodes.Count > 0);

                if (model.IsValid)
                {
                    for (int i = 0; i < model.ScalerStd.Length; i++)
                        if (model.ScalerStd[i] < 1e-6f) model.ScalerStd[i] = 1f;
                }

                return model.IsValid ? new TreeEnsemblePredictor(model) : null;
            }
            catch
            {
                return null;
            }
        }

        public double Predict(float[] features)
        {
            if (_model == null || !_model.IsValid || features == null) return double.NaN;
            if (features.Length != BotFeatureExtractor.FeatureDim) return double.NaN;

            var scaled = new float[features.Length];
            for (int i = 0; i < features.Length; i++)
                scaled[i] = (features[i] - _model.ScalerMean[i]) / _model.ScalerStd[i];

            double sum = 0;
            foreach (var tree in _model.Trees)
                sum += WalkTree(tree, scaled);

            return sum / _model.Trees.Count;
        }

        private static float WalkTree(FlatTree tree, float[] x)
        {
            int nodeId = tree.Root;
            int guard = 0;
            while (nodeId >= 0 && nodeId < tree.Nodes.Count && guard++ < 128)
            {
                var node = tree.Nodes[nodeId];
                if (node.IsLeaf) return node.Value;
                nodeId = x[node.Feature] <= node.Threshold ? node.Left : node.Right;
            }
            return 0f;
        }
    }
}
