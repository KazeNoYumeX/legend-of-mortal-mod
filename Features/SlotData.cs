using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace LegendOfMortalMod.Features
{
    // 依存檔欄位記的小資料（存檔備註、劇情中存檔的背景與音樂），另存在 BepInEx\config，不寫進遊戲存檔，
    // 移除 mod 後存檔不受影響。每行「欄位\t名稱\t值」，有變更才寫檔
    internal static class SlotData
    {
        private static readonly string FilePath = Path.Combine(Paths.ConfigPath, "LegendOfMortalMod.slots.txt");
        private static Dictionary<string, string> _values;

        private static Dictionary<string, string> Values
        {
            get
            {
                if (_values != null)
                    return _values;
                _values = new Dictionary<string, string>();
                if (File.Exists(FilePath))
                {
                    foreach (string line in File.ReadAllLines(FilePath))
                    {
                        string[] parts = line.Split(new[] { '\t' }, 3);
                        if (parts.Length == 3)
                            _values[parts[0] + "\t" + parts[1]] = parts[2];
                    }
                }
                return _values;
            }
        }

        public static string Get(string slot, string name) =>
            slot != null && Values.TryGetValue(slot + "\t" + name, out string value) ? value : "";

        // 空字串就刪掉這筆
        public static void Set(string slot, string name, string value)
        {
            if (slot == null)
                return;
            value = (value ?? "").Replace("\t", " ").Replace("\r", "").Replace("\n", " ");
            string key = slot + "\t" + name;
            if (Get(slot, name) == value)
                return;
            if (value.Length == 0)
                Values.Remove(key);
            else
                Values[key] = value;
            Write();
        }

        public static void Remove(string slot)
        {
            List<string> keys = Values.Keys.Where(key => key.StartsWith(slot + "\t")).ToList();
            if (keys.Count == 0)
                return;
            foreach (string key in keys)
                Values.Remove(key);
            Write();
        }

        private static void Write()
        {
            try
            {
                File.WriteAllLines(
                    FilePath,
                    Values.OrderBy(pair => pair.Key).Select(pair => pair.Key + "\t" + pair.Value)
                );
            }
            catch (IOException ex)
            {
                Plugin.Log.LogWarning("SlotData: " + ex.Message);
            }
        }
    }
}
