using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenDDZ.DDZUtils.AI
{
    /// <summary>
    /// DDZM v3 binary tree ensemble format.
    /// </summary>
    public static class BinaryModelFormat
    {
        public const int Version = 3;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("DDZM");

        public static void Write(string path, TreeEnsembleModel model)
        {
            if (model == null || !model.IsValid)
                throw new ArgumentException("Invalid model");

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(Magic);
                w.Write(model.Version);
                w.Write(model.ScalerMean.Length);
                w.Write(model.Trees.Count);

                foreach (var v in model.ScalerMean) w.Write(v);
                foreach (var v in model.ScalerStd) w.Write(v);

                foreach (var tree in model.Trees)
                {
                    w.Write(tree.Nodes.Count);
                    foreach (var node in tree.Nodes)
                    {
                        w.Write((short)node.Feature);
                        w.Write((short)0);
                        w.Write(node.Threshold);
                        w.Write(node.Left);
                        w.Write(node.Right);
                        w.Write(node.Value);
                    }
                }
            }
        }

        public static TreeEnsembleModel Read(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var r = new BinaryReader(fs))
            {
                var magic = r.ReadBytes(4);
                if (!magic.SequenceEqual(Magic))
                    return null;

                int version = r.ReadInt32();
                if (version != Version)
                    return null;

                int featureDim = r.ReadInt32();
                int treeCount = r.ReadInt32();
                if (featureDim <= 0 || treeCount <= 0)
                    return null;

                var mean = new float[featureDim];
                var std = new float[featureDim];
                for (int i = 0; i < featureDim; i++) mean[i] = r.ReadSingle();
                for (int i = 0; i < featureDim; i++) std[i] = r.ReadSingle();

                var model = new TreeEnsembleModel
                {
                    Version = version,
                    FeatureNames = BotFeatureExtractor.FeatureNames,
                    ScalerMean = mean,
                    ScalerStd = std,
                    Trees = new List<FlatTree>()
                };

                for (int t = 0; t < treeCount; t++)
                {
                    int nodeCount = r.ReadInt32();
                    if (nodeCount <= 0) return null;

                    var tree = new FlatTree { Root = 0, Nodes = new List<TreeNode>(nodeCount) };
                    for (int n = 0; n < nodeCount; n++)
                    {
                        int feature = r.ReadInt16();
                        r.ReadInt16(); // pad
                        float threshold = r.ReadSingle();
                        int left = r.ReadInt32();
                        int right = r.ReadInt32();
                        float value = r.ReadSingle();

                        tree.Nodes.Add(new TreeNode
                        {
                            Feature = feature,
                            Threshold = threshold,
                            Left = left,
                            Right = right,
                            Value = value
                        });
                    }
                    model.Trees.Add(tree);
                }

                model.IsValid = model.Trees.Count > 0 &&
                    model.ScalerMean.Length == BotFeatureExtractor.FeatureDim &&
                    model.ScalerStd.Length == BotFeatureExtractor.FeatureDim &&
                    model.Trees.All(tr => tr.Nodes.Count > 0);

                if (model.IsValid)
                {
                    for (int i = 0; i < model.ScalerStd.Length; i++)
                        if (model.ScalerStd[i] < 1e-6f) model.ScalerStd[i] = 1f;
                }

                return model.IsValid ? model : null;
            }
        }
    }
}
