using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace ZwcadBatchPlot;

public sealed class ImageStampEntry
{
    public string Name { get; set; } = "";
    public string PngBase64 { get; set; } = "";
    public bool Enabled { get; set; } = true;
    [JsonIgnore] public string PixelSize
    {
        get { using var stream = new MemoryStream(Convert.FromBase64String(PngBase64)); using var image = System.Drawing.Image.FromStream(stream); return $"{image.Width} × {image.Height}"; }
    }
}

/// <summary>Portable image library; image bytes travel with the JSON, independently of the source image path.</summary>
public static class ImageStampLibrary
{
    public static ImageStampEntry FromImage(string path)
    {
        using var source = System.Drawing.Image.FromFile(path);
        using var bytes = new MemoryStream();
        source.Save(bytes, System.Drawing.Imaging.ImageFormat.Png);
        return new ImageStampEntry { Name = Path.GetFileNameWithoutExtension(path), PngBase64 = Convert.ToBase64String(bytes.ToArray()) };
    }
    public static List<ImageStampEntry> Load(string path)
    {
        if (!File.Exists(path)) return new List<ImageStampEntry>();
        var entries = JsonConvert.DeserializeObject<List<ImageStampEntry>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("印章库为空或格式不正确。");
        foreach (var entry in entries) Validate(entry);
        return entries;
    }
    public static void Save(string path, IEnumerable<ImageStampEntry> entries)
    {
        var list = entries.ToList();
        foreach (var entry in list) Validate(entry);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonConvert.SerializeObject(list, Formatting.Indented));
    }
    public static void Validate(ImageStampEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.Name)) throw new InvalidDataException("请输入印章名称。");
        using var stream = new MemoryStream(Convert.FromBase64String(entry.PngBase64));
        using var image = System.Drawing.Image.FromStream(stream);
        if (image.Width < 1 || image.Height < 1) throw new InvalidDataException("印章图片无效。");
    }
    public static int Merge(ICollection<ImageStampEntry> current, IEnumerable<ImageStampEntry> incoming)
    {
        var list = incoming.ToList();
        foreach (var entry in list) Validate(entry);
        int count = 0;
        foreach (var entry in list) {
            if (current.Any(x => x.PngBase64 == entry.PngBase64)) continue;
            current.Add(entry); count++;
        }
        return count;
    }
    public static string Materialize(ImageStampEntry entry, string directory)
    {
        Validate(entry);
        if (!entry.Enabled) throw new InvalidOperationException("此印章已停用，请选择启用的印章。");
        byte[] bytes = Convert.FromBase64String(entry.PngBase64);
        using var sha = SHA256.Create();
        string name = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "") + ".png";
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
