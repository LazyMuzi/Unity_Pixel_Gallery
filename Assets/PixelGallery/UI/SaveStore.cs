using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace PixelGallery
{
    public sealed class SaveStore
    {
        readonly string path;
        bool recovered;
        public bool RecoveredFromBackup => recovered;
        public SaveStore(string path) { this.path = path; }
        public SaveData Load()
        {
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return new SaveData();
            try { return Read(path); }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException)
            {
                if (!File.Exists(path + ".bak")) throw new InvalidDataException("저장 파일을 읽을 수 없습니다. 기존 파일은 보존했습니다.", error);
                var data = Read(path + ".bak"); recovered = true; return data;
            }
        }
        static SaveData Read(string file)
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(file));
            if (data == null) throw new InvalidDataException("Empty save.");
            // JsonUtility serializes null inline objects as empty objects.
            if (!data.HasPending) data.Pending = null;
            if (data.Version == 1)
            {
                data.Version = 2; data.Colors = new System.Collections.Generic.List<int> { 1,2,3,4,5,6,7,8 };
                data.Tools = 0; data.SeenNpcs = new System.Collections.Generic.List<string>();
                if (data.Collection == null) throw new InvalidDataException("Missing collection.");
                foreach (var artwork in data.Collection)
                    if (artwork != null && artwork.LastShow != null && artwork.LastShow.Judgment != null && artwork.LastShow.Judgment.Visitors != null)
                        foreach (var visitor in artwork.LastShow.Judgment.Visitors)
                            if (visitor != null && !string.IsNullOrEmpty(visitor.Id))
                            {
                                string id = visitor.Id.Split('#')[0];
                                if (!data.SeenNpcs.Contains(id)) data.SeenNpcs.Add(id);
                            }
            }
            data.Validate(); return data;
        }
        public void Write(SaveData data)
        {
            data.Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, recovered ? null : path + ".bak");
            else File.Move(temp, path);
            recovered = false;
        }
    }
}
