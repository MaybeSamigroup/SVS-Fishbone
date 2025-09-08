using System.IO;
using System.Linq;
using Fishbone;

args.Where(File.Exists).ToList()
    .Select(path => (
        path: $"{Path.GetDirectoryName(path)}/{Path.GetFileNameWithoutExtension(path)}.zip",
        data: Decode.Extract(File.ReadAllBytes(path))))
    .Where(pair => pair.data.Length > 0)
    .ToList().ForEach(pair => File.WriteAllBytes(pair.path, pair.data));
return 0;